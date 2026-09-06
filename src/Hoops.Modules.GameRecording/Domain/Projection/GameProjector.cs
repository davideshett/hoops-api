using Hoops.SharedKernel;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.GameRecording.Domain.Projection;

/// <summary>One player in the game context, as frozen at roster lock.</summary>
/// <param name="GameRosterEntryId">The snapshot row id — how events address a player.</param>
/// <param name="CompetitionTeamId">Their team.</param>
/// <param name="PlayerId">The registry player.</param>
/// <param name="IsStarter">Whether they started.</param>
public sealed record GamePlayer(
    GameRosterEntryId GameRosterEntryId, CompetitionTeamId CompetitionTeamId, PlayerId PlayerId, bool IsStarter);

/// <summary>The immutable context a game is projected against: its teams, roster, and frozen rules.</summary>
/// <param name="GameId">The game.</param>
/// <param name="HomeCompetitionTeamId">Home team.</param>
/// <param name="AwayCompetitionTeamId">Away team.</param>
/// <param name="Players">The frozen game roster.</param>
/// <param name="RuleSet">The rule set snapshotted at roster lock.</param>
public sealed record GameContext(
    GameId GameId,
    CompetitionTeamId HomeCompetitionTeamId,
    CompetitionTeamId AwayCompetitionTeamId,
    IReadOnlyList<GamePlayer> Players,
    RuleSet RuleSet);

/// <summary>Projects an ordered event log into derived game state.</summary>
public interface IGameProjector
{
    /// <summary>
    /// Replays <paramref name="events"/> in sequence order and returns the derived state. PURE: no
    /// I/O, no ambient time, no randomness — identical input always yields identical output.
    /// </summary>
    GameProjection Project(GameContext context, IReadOnlyList<GameEvent> events);
}

/// <summary>
/// The projector (ADR-001, §9.1). Everything the product sells — box scores, leaderboards, career
/// totals — is derived here from the event log alone.
///
/// PURITY IS A HARD REQUIREMENT: this type must never reference a DbContext, DateTime.Now, or Random.
/// An architecture test enforces that. Voided events are skipped, which is what makes "undo" safe at
/// the scorer's table.
/// </summary>
public sealed class GameProjector : IGameProjector
{
    /// <inheritdoc />
    public GameProjection Project(GameContext context, IReadOnlyList<GameEvent> events)
    {
        var state = new ProjectionState(context);

        foreach (var e in events.OrderBy(e => e.Sequence))
        {
            if (e.IsVoided)
            {
                continue; // voided events contribute nothing, but their rows remain in the log
            }

            state.Apply(e);
        }

        return state.ToProjection();
    }

    /// <summary>Mutable accumulator used only within a single <see cref="Project"/> call.</summary>
    private sealed class ProjectionState
    {
        private readonly GameContext _context;
        private readonly Dictionary<GameRosterEntryId, PlayerAccumulator> _players = [];
        private readonly Dictionary<CompetitionTeamId, TeamAccumulator> _teams = [];
        private readonly Dictionary<CompetitionTeamId, HashSet<GameRosterEntryId>> _onCourt = [];
        private readonly List<PeriodState> _completedPeriods = [];
        private readonly List<LineupStint> _stints = [];
        private readonly Dictionary<CompetitionTeamId, OpenStint> _openStints = [];
        private readonly Dictionary<CompetitionTeamId, int> _periodPoints = [];
        private readonly Dictionary<CompetitionTeamId, int> _periodTeamFouls = [];

        private long _lastSequence;
        private int _currentPeriod;
        private int _gameClockMs;
        private bool _clockRunning;
        private bool _gameStarted;
        private bool _gameEnded;
        private bool _periodEnded;

        // Clock bookkeeping for seconds-played: the clock reading when the running span began.
        private int _spanStartClockMs;

        public ProjectionState(GameContext context)
        {
            _context = context;

            foreach (var team in Teams)
            {
                _teams[team] = new TeamAccumulator();
                _onCourt[team] = [];
                _periodPoints[team] = 0;
                _periodTeamFouls[team] = 0;
            }

            foreach (var player in context.Players)
            {
                _players[player.GameRosterEntryId] = new PlayerAccumulator(player);
            }

            _gameClockMs = context.RuleSet.PeriodDurationSeconds * 1000;
        }

        private IEnumerable<CompetitionTeamId> Teams
        {
            get
            {
                yield return _context.HomeCompetitionTeamId;
                yield return _context.AwayCompetitionTeamId;
            }
        }

        public void Apply(GameEvent e)
        {
            _lastSequence = Math.Max(_lastSequence, e.Sequence);

            // Advance the clock-derived playing time before applying the event's own effect.
            AdvanceClock(e);

            switch (e.EventType)
            {
                case EventTypes.GameStart:
                    ApplyGameStart();
                    break;
                case EventTypes.PeriodStart:
                    ApplyPeriodStart(e);
                    break;
                case EventTypes.PeriodEnd:
                    ApplyPeriodEnd();
                    break;
                case EventTypes.GameEnd:
                    CloseStints();
                    _gameEnded = true;
                    _clockRunning = false;
                    break;
                case EventTypes.FieldGoalMade:
                    ApplyFieldGoalMade(e);
                    break;
                case EventTypes.FieldGoalMissed:
                    ApplyFieldGoalMissed(e);
                    break;
                case EventTypes.FreeThrowMade:
                    ApplyFreeThrow(e, made: true);
                    break;
                case EventTypes.FreeThrowMissed:
                    ApplyFreeThrow(e, made: false);
                    break;
                case EventTypes.Rebound:
                    ApplyRebound(e);
                    break;
                case EventTypes.TeamRebound:
                    ApplyTeamRebound(e);
                    break;
                case EventTypes.Turnover:
                    ApplyTurnover(e);
                    break;
                case EventTypes.Foul:
                    ApplyFoul(e);
                    break;
                case EventTypes.Substitution:
                    ApplySubstitution(e);
                    break;
                case EventTypes.Timeout:
                    ApplyTimeout(e);
                    break;
                case EventTypes.ClockStart:
                    StartClock(e);
                    break;
                case EventTypes.ClockStop:
                    StopClock(e);
                    break;

                // JUMP_BALL, VOID, and NOTE contribute nothing to the statline.
            }
        }

        // ── clock & minutes ──────────────────────────────────────────────────

        private void AdvanceClock(GameEvent e)
        {
            if (_clockRunning && e.Period == _currentPeriod)
            {
                // The clock counts DOWN, so elapsed time is the decrease in the reading.
                var elapsedMs = _spanStartClockMs - e.GameClockMs;
                if (elapsedMs > 0)
                {
                    CreditTimeOnCourt(elapsedMs);
                }

                _spanStartClockMs = e.GameClockMs;
            }

            if (e.Period == _currentPeriod)
            {
                _gameClockMs = e.GameClockMs;
            }
        }

        private void CreditTimeOnCourt(int elapsedMs)
        {
            foreach (var team in Teams)
            {
                foreach (var id in _onCourt[team])
                {
                    if (_players.TryGetValue(id, out var player))
                    {
                        player.MillisecondsPlayed += elapsedMs;
                    }
                }

                if (_openStints.TryGetValue(team, out var stint))
                {
                    stint.MillisecondsPlayed += elapsedMs;
                }
            }
        }

        private void StartClock(GameEvent e)
        {
            _clockRunning = true;
            _spanStartClockMs = e.GameClockMs;
        }

        private void StopClock(GameEvent e)
        {
            _clockRunning = false;
            _spanStartClockMs = e.GameClockMs;
        }

        // ── flow ─────────────────────────────────────────────────────────────

        private void ApplyGameStart()
        {
            _gameStarted = true;
            foreach (var player in _context.Players.Where(p => p.IsStarter))
            {
                _onCourt[player.CompetitionTeamId].Add(player.GameRosterEntryId);
            }

            OpenStints();
        }

        private void ApplyPeriodStart(GameEvent e)
        {
            _currentPeriod = e.Period;
            _periodEnded = false;
            _clockRunning = false;

            var isOvertime = string.Equals(e.EventSubtype, EventSubtypes.Overtime, StringComparison.Ordinal);
            _gameClockMs = (isOvertime ? _context.RuleSet.OvertimeDurationSeconds : _context.RuleSet.PeriodDurationSeconds) * 1000;
            _spanStartClockMs = _gameClockMs;

            foreach (var team in Teams)
            {
                _periodPoints[team] = 0;
                _periodTeamFouls[team] = 0; // team fouls reset each period
            }

            OpenStints();
        }

        private void ApplyPeriodEnd()
        {
            CloseStints();
            _periodEnded = true;
            _clockRunning = false;
            _completedPeriods.Add(new PeriodState
            {
                Period = _currentPeriod,
                IsComplete = true,
                PointsByTeam = new Dictionary<CompetitionTeamId, int>(_periodPoints),
                TeamFoulsByTeam = new Dictionary<CompetitionTeamId, int>(_periodTeamFouls),
            });
        }

        // ── scoring ──────────────────────────────────────────────────────────

        private void ApplyFieldGoalMade(GameEvent e)
        {
            var isThree = string.Equals(e.EventSubtype, EventSubtypes.ThreePoint, StringComparison.Ordinal);
            var points = e.Points ?? (isThree ? 3 : 2);

            if (Player(e.GameRosterEntryId) is { } shooter)
            {
                shooter.Points += points;
                shooter.FieldGoalsMade++;
                shooter.FieldGoalsAttempted++;
                if (isThree)
                {
                    // A made three increments FGM, FGA, 3PM and 3PA — threes are a subset, not additive.
                    shooter.ThreePointersMade++;
                    shooter.ThreePointersAttempted++;
                }

                Score(shooter.Player.CompetitionTeamId, points);
                var team = _teams[shooter.Player.CompetitionTeamId];
                team.FieldGoalsMade++;
                team.FieldGoalsAttempted++;
                if (isThree)
                {
                    team.ThreePointersMade++;
                    team.ThreePointersAttempted++;
                }
            }

            // ADR-002: the assist is the secondary participant on a made basket.
            if (Player(e.SecondaryRosterEntryId) is { } assister)
            {
                assister.Assists++;
                _teams[assister.Player.CompetitionTeamId].Assists++;
            }
        }

        private void ApplyFieldGoalMissed(GameEvent e)
        {
            var isThree = string.Equals(e.EventSubtype, EventSubtypes.ThreePoint, StringComparison.Ordinal);

            if (Player(e.GameRosterEntryId) is { } shooter)
            {
                shooter.FieldGoalsAttempted++;
                if (isThree)
                {
                    shooter.ThreePointersAttempted++;
                }

                var team = _teams[shooter.Player.CompetitionTeamId];
                team.FieldGoalsAttempted++;
                if (isThree)
                {
                    team.ThreePointersAttempted++;
                }
            }

            // ADR-002: the block is the secondary participant on a missed shot.
            if (Player(e.SecondaryRosterEntryId) is { } blocker)
            {
                blocker.Blocks++;
                _teams[blocker.Player.CompetitionTeamId].Blocks++;
                if (Player(e.GameRosterEntryId) is { } blocked)
                {
                    blocked.BlocksAgainst++;
                }
            }
        }

        private void ApplyFreeThrow(GameEvent e, bool made)
        {
            if (Player(e.GameRosterEntryId) is not { } shooter)
            {
                return;
            }

            shooter.FreeThrowsAttempted++;
            var team = _teams[shooter.Player.CompetitionTeamId];
            team.FreeThrowsAttempted++;

            if (made)
            {
                shooter.FreeThrowsMade++;
                shooter.Points += 1;
                team.FreeThrowsMade++;
                Score(shooter.Player.CompetitionTeamId, 1);
            }
        }

        // ── rebounds, turnovers, fouls ───────────────────────────────────────

        private void ApplyRebound(GameEvent e)
        {
            if (Player(e.GameRosterEntryId) is not { } player)
            {
                return;
            }

            var offensive = string.Equals(e.EventSubtype, EventSubtypes.Offensive, StringComparison.Ordinal);
            var team = _teams[player.Player.CompetitionTeamId];
            if (offensive)
            {
                player.OffensiveRebounds++;
                team.OffensiveRebounds++;
            }
            else
            {
                player.DefensiveRebounds++;
                team.DefensiveRebounds++;
            }
        }

        private void ApplyTeamRebound(GameEvent e)
        {
            if (e.CompetitionTeamId is not { } teamId || !_teams.TryGetValue(teamId, out var team))
            {
                return;
            }

            if (string.Equals(e.EventSubtype, EventSubtypes.Offensive, StringComparison.Ordinal))
            {
                team.OffensiveRebounds++;
            }
            else if (string.Equals(e.EventSubtype, EventSubtypes.Defensive, StringComparison.Ordinal))
            {
                team.DefensiveRebounds++;
            }

            // DeadBall team rebounds are recorded but count toward neither column.
        }

        private void ApplyTurnover(GameEvent e)
        {
            if (Player(e.GameRosterEntryId) is { } committer)
            {
                committer.Turnovers++;
                _teams[committer.Player.CompetitionTeamId].Turnovers++;
            }

            // ADR-002: the steal is the secondary participant on a turnover.
            if (Player(e.SecondaryRosterEntryId) is { } stealer)
            {
                stealer.Steals++;
                _teams[stealer.Player.CompetitionTeamId].Steals++;
            }
        }

        private void ApplyFoul(GameEvent e)
        {
            var subtype = e.EventSubtype ?? EventSubtypes.Personal;

            if (EventSubtypes.BenchFouls.Contains(subtype))
            {
                // Bench and coach technicals charge the team only, never a player.
                if (e.CompetitionTeamId is { } benchTeam && _teams.ContainsKey(benchTeam))
                {
                    _teams[benchTeam].FoulsCommitted++;
                    _periodTeamFouls[benchTeam]++;
                }
            }
            else if (Player(e.GameRosterEntryId) is { } offender)
            {
                offender.FoulsCommitted++;
                if (string.Equals(subtype, EventSubtypes.Technical, StringComparison.Ordinal))
                {
                    offender.TechnicalFouls++;
                }

                var teamId = offender.Player.CompetitionTeamId;
                _teams[teamId].FoulsCommitted++;
                _periodTeamFouls[teamId]++;

                var limit = _context.RuleSet.PersonalFoulLimit;
                var technicalLimit = _context.RuleSet.TechnicalFoulLimit;
                if (offender.FoulsCommitted >= limit || offender.TechnicalFouls >= technicalLimit)
                {
                    offender.FouledOut = true;
                    if (_onCourt[teamId].Contains(offender.Player.GameRosterEntryId))
                    {
                        CloseStint(teamId, e.GameClockMs);
                        _onCourt[teamId].Remove(offender.Player.GameRosterEntryId);
                        OpenStint(teamId, e.GameClockMs);
                    }
                }
            }

            if (Player(e.SecondaryRosterEntryId) is { } fouled)
            {
                fouled.FoulsDrawn++;
            }
        }

        private void ApplySubstitution(GameEvent e)
        {
            // A lineup change ends the current stint and starts a new one for that team.
            var affected = Player(e.GameRosterEntryId)?.Player.CompetitionTeamId
                ?? Player(e.SecondaryRosterEntryId)?.Player.CompetitionTeamId;
            if (affected is { } teamId)
            {
                CloseStint(teamId, e.GameClockMs);
            }

            // Primary is the player going OUT, secondary the player coming IN.
            if (Player(e.GameRosterEntryId) is { } outgoing)
            {
                _onCourt[outgoing.Player.CompetitionTeamId].Remove(outgoing.Player.GameRosterEntryId);
            }

            if (Player(e.SecondaryRosterEntryId) is { } incoming)
            {
                _onCourt[incoming.Player.CompetitionTeamId].Add(incoming.Player.GameRosterEntryId);
            }

            if (affected is { } reopened)
            {
                OpenStint(reopened, e.GameClockMs);
            }
        }

        private void ApplyTimeout(GameEvent e)
        {
            if (e.CompetitionTeamId is { } teamId && _teams.TryGetValue(teamId, out var team))
            {
                team.TimeoutsTaken++;
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private PlayerAccumulator? Player(GameRosterEntryId? id)
            => id.HasValue && _players.TryGetValue(id.Value, out var p) ? p : null;

        private void Score(CompetitionTeamId teamId, int points)
        {
            _teams[teamId].Points += points;
            _periodPoints[teamId] += points;

            // Plus/minus: everyone on court gains for their own team and loses for the opponent's.
            foreach (var team in Teams)
            {
                var delta = team == teamId ? points : -points;
                foreach (var id in _onCourt[team])
                {
                    if (_players.TryGetValue(id, out var player))
                    {
                        player.PlusMinus += delta;
                    }
                }

                if (_openStints.TryGetValue(team, out var stint))
                {
                    if (team == teamId)
                    {
                        stint.PointsFor += points;
                    }
                    else
                    {
                        stint.PointsAgainst += points;
                    }
                }
            }
        }

        // ── lineup stints ────────────────────────────────────────────────────

        private void OpenStints()
        {
            foreach (var team in Teams)
            {
                OpenStint(team, _gameClockMs);
            }
        }

        private void OpenStint(CompetitionTeamId teamId, int clockMs)
            => _openStints[teamId] = new OpenStint
            {
                Players = _onCourt[teamId].OrderBy(x => x.Value).ToList(),
                Period = _currentPeriod,
                StartClockMs = clockMs,
            };

        private void CloseStints()
        {
            foreach (var team in Teams.ToList())
            {
                CloseStint(team, _gameClockMs);
            }
        }

        private void CloseStint(CompetitionTeamId teamId, int clockMs)
        {
            if (!_openStints.Remove(teamId, out var stint))
            {
                return;
            }

            // A stint with no elapsed time and no scoring carries no information; drop it so
            // back-to-back substitutions do not litter the output with empty spans.
            if (stint.MillisecondsPlayed == 0 && stint.PointsFor == 0 && stint.PointsAgainst == 0)
            {
                return;
            }

            _stints.Add(new LineupStint
            {
                CompetitionTeamId = teamId,
                Players = stint.Players,
                Period = stint.Period,
                StartClockMs = stint.StartClockMs,
                EndClockMs = clockMs,
                SecondsPlayed = stint.MillisecondsPlayed / 1000,
                PointsFor = stint.PointsFor,
                PointsAgainst = stint.PointsAgainst,
            });
        }

        public GameProjection ToProjection()
        {
            var score = _teams.ToDictionary(kv => kv.Key, kv => kv.Value.Points);

            var periods = new List<PeriodState>(_completedPeriods);
            if (_currentPeriod > 0 && !_periodEnded)
            {
                periods.Add(new PeriodState
                {
                    Period = _currentPeriod,
                    IsComplete = false,
                    PointsByTeam = new Dictionary<CompetitionTeamId, int>(_periodPoints),
                    TeamFoulsByTeam = new Dictionary<CompetitionTeamId, int>(_periodTeamFouls),
                });
            }

            var live = new LiveGameState
            {
                GameId = _context.GameId,
                LastSequence = _lastSequence,
                CurrentPeriod = _currentPeriod,
                GameClockMs = _gameClockMs,
                ClockRunning = _clockRunning,
                GameStarted = _gameStarted,
                GameEnded = _gameEnded,
                PeriodEnded = _periodEnded,
                Score = score,
                TeamFouls = new Dictionary<CompetitionTeamId, int>(_periodTeamFouls),
                TimeoutsRemaining = _teams.ToDictionary(kv => kv.Key, kv => TimeoutsRemaining(kv.Value)),
                OnCourt = _onCourt.ToDictionary(
                    kv => kv.Key,
                    kv => (IReadOnlyList<GameRosterEntryId>)kv.Value.OrderBy(x => x.Value).ToList()),
                FouledOut = _players.Values.Where(p => p.FouledOut)
                    .Select(p => p.Player.GameRosterEntryId).OrderBy(x => x.Value).ToList(),
            };

            var projection = new GameProjection
            {
                PlayerStatlines = _players.Values
                    .OrderBy(p => p.Player.CompetitionTeamId.Value).ThenBy(p => p.Player.GameRosterEntryId.Value)
                    .Select(p => p.ToStatline()).ToList(),
                TeamStatlines = _teams.OrderBy(kv => kv.Key.Value).Select(kv => kv.Value.ToStatline(kv.Key)).ToList(),
                Periods = periods,
                LineupStints = _stints,
                Score = score,
                LiveState = live,
            };

            AssertInvariants(projection);
            return projection;
        }

        /// <summary>
        /// Arithmetic that must hold for any correctly projected game. Checked here, at the moment of
        /// creation, rather than only in tests — a violation means the projector itself is wrong, and
        /// failing loudly beats writing a silently corrupt statline to the permanent record.
        /// </summary>
        private static void AssertInvariants(GameProjection projection)
        {
            foreach (var team in projection.TeamStatlines)
            {
                var playerPoints = projection.PlayerStatlines
                    .Where(p => p.CompetitionTeamId == team.CompetitionTeamId)
                    .Sum(p => p.Points);
                if (playerPoints != team.Points)
                {
                    throw new InvalidOperationException(
                        $"Projection invariant violated: team {team.CompetitionTeamId} has {team.Points} points "
                        + $"but its players sum to {playerPoints}.");
                }
            }

            // Every basket credits one side and debits the other, so the whole game nets to zero.
            var netPlusMinus = projection.PlayerStatlines.Sum(p => p.PlusMinus);
            if (netPlusMinus != 0)
            {
                throw new InvalidOperationException(
                    $"Projection invariant violated: plus/minus across all players is {netPlusMinus}, expected 0.");
            }
        }

        private int TimeoutsRemaining(TeamAccumulator team)
        {
            var rules = _context.RuleSet;
            var allowance = rules.TimeoutsFirstHalf + rules.TimeoutsSecondHalf;
            if (_currentPeriod > rules.NumberOfPeriods)
            {
                allowance += (_currentPeriod - rules.NumberOfPeriods) * rules.TimeoutsPerOvertime;
            }

            return Math.Max(0, allowance - team.TimeoutsTaken);
        }
    }

    /// <summary>A stint being accumulated; becomes a <see cref="LineupStint"/> when it closes.</summary>
    private sealed class OpenStint
    {
        public required IReadOnlyList<GameRosterEntryId> Players { get; init; }

        public required int Period { get; init; }

        public required int StartClockMs { get; init; }

        public int MillisecondsPlayed;
        public int PointsFor;
        public int PointsAgainst;
    }

    private sealed class PlayerAccumulator(GamePlayer player)
    {
        public GamePlayer Player { get; } = player;

        public int Points;
        public int FieldGoalsMade;
        public int FieldGoalsAttempted;
        public int ThreePointersMade;
        public int ThreePointersAttempted;
        public int FreeThrowsMade;
        public int FreeThrowsAttempted;
        public int OffensiveRebounds;
        public int DefensiveRebounds;
        public int Assists;
        public int Steals;
        public int Blocks;
        public int BlocksAgainst;
        public int Turnovers;
        public int FoulsCommitted;
        public int TechnicalFouls;
        public int FoulsDrawn;
        public bool FouledOut;
        public int MillisecondsPlayed;
        public int PlusMinus;

        public PlayerStatline ToStatline() => new()
        {
            GameRosterEntryId = Player.GameRosterEntryId,
            CompetitionTeamId = Player.CompetitionTeamId,
            PlayerId = Player.PlayerId,
            Points = Points,
            FieldGoalsMade = FieldGoalsMade,
            FieldGoalsAttempted = FieldGoalsAttempted,
            ThreePointersMade = ThreePointersMade,
            ThreePointersAttempted = ThreePointersAttempted,
            FreeThrowsMade = FreeThrowsMade,
            FreeThrowsAttempted = FreeThrowsAttempted,
            OffensiveRebounds = OffensiveRebounds,
            DefensiveRebounds = DefensiveRebounds,
            Assists = Assists,
            Steals = Steals,
            Blocks = Blocks,
            BlocksAgainst = BlocksAgainst,
            Turnovers = Turnovers,
            FoulsCommitted = FoulsCommitted,
            FoulsDrawn = FoulsDrawn,
            FouledOut = FouledOut,
            SecondsPlayed = MillisecondsPlayed / 1000,
            PlusMinus = PlusMinus,
        };
    }

    private sealed class TeamAccumulator
    {
        public int Points;
        public int FieldGoalsMade;
        public int FieldGoalsAttempted;
        public int ThreePointersMade;
        public int ThreePointersAttempted;
        public int FreeThrowsMade;
        public int FreeThrowsAttempted;
        public int OffensiveRebounds;
        public int DefensiveRebounds;
        public int Assists;
        public int Steals;
        public int Blocks;
        public int Turnovers;
        public int FoulsCommitted;
        public int TimeoutsTaken;

        public TeamStatline ToStatline(CompetitionTeamId id) => new()
        {
            CompetitionTeamId = id,
            Points = Points,
            FieldGoalsMade = FieldGoalsMade,
            FieldGoalsAttempted = FieldGoalsAttempted,
            ThreePointersMade = ThreePointersMade,
            ThreePointersAttempted = ThreePointersAttempted,
            FreeThrowsMade = FreeThrowsMade,
            FreeThrowsAttempted = FreeThrowsAttempted,
            OffensiveRebounds = OffensiveRebounds,
            DefensiveRebounds = DefensiveRebounds,
            Assists = Assists,
            Steals = Steals,
            Blocks = Blocks,
            Turnovers = Turnovers,
            FoulsCommitted = FoulsCommitted,
            TimeoutsTaken = TimeoutsTaken,
        };
    }
}
