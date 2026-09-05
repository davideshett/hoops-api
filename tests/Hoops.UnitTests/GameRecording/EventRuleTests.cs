using FluentAssertions;
using Hoops.Modules.GameRecording.Domain;
using Hoops.Modules.GameRecording.Domain.Projection;
using Hoops.Modules.GameRecording.Domain.Validation;
using Hoops.SharedKernel;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.UnitTests.GameRecording;

/// <summary>
/// One positive and one negative test for every rule code in §10. These are what let the recording UI
/// be aggressive and one-tap without producing garbage history.
/// </summary>
public sealed class EventRuleTests
{
    private static readonly CompetitionTeamId Home = GoldenFiles.Home;
    private static readonly CompetitionTeamId Away = GoldenFiles.Away;

    // Home: 1..5 starters, 6 bench. Away: 11..15 starters.
    private static GameContext Context(RuleSet? rules = null) => new(
        GameId.FromGuid(new Guid("00000000-0000-0000-0000-0000000000f1")),
        Home, Away,
        [
            .. new[] { 1, 2, 3, 4, 5 }.Select(i => new GamePlayer(GoldenFiles.Roster(i), Home, GoldenFiles.Player(i), true)),
            new GamePlayer(GoldenFiles.Roster(6), Home, GoldenFiles.Player(6), false),
            .. new[] { 11, 12, 13, 14, 15 }.Select(i => new GamePlayer(GoldenFiles.Roster(i), Away, GoldenFiles.Player(i), true)),
        ],
        rules ?? RuleSet.Fiba());

    /// <summary>A live state with both fives on court and the game under way.</summary>
    private static LiveGameState LiveState(
        bool started = true, bool clockRunning = false, IReadOnlyList<GameRosterEntryId>? fouledOut = null,
        int homeScore = 10, int awayScore = 8, int timeouts = 5)
        => new()
        {
            GameId = GameId.FromGuid(new Guid("00000000-0000-0000-0000-0000000000f1")),
            LastSequence = 10,
            CurrentPeriod = 1,
            GameClockMs = 300000,
            ClockRunning = clockRunning,
            GameStarted = started,
            GameEnded = false,
            PeriodEnded = false,
            Score = new Dictionary<CompetitionTeamId, int> { [Home] = homeScore, [Away] = awayScore },
            TeamFouls = new Dictionary<CompetitionTeamId, int> { [Home] = 0, [Away] = 0 },
            TimeoutsRemaining = new Dictionary<CompetitionTeamId, int> { [Home] = timeouts, [Away] = timeouts },
            OnCourt = new Dictionary<CompetitionTeamId, IReadOnlyList<GameRosterEntryId>>
            {
                [Home] = new[] { 1, 2, 3, 4, 5 }.Select(GoldenFiles.Roster).ToList(),
                [Away] = new[] { 11, 12, 13, 14, 15 }.Select(GoldenFiles.Roster).ToList(),
            },
            FouledOut = fouledOut ?? [],
        };

    private static ValidationContext Ctx(
        LiveGameState? state = null, GameStatus status = GameStatus.InProgress,
        GameEvent? previous = null, int freeThrowsAwarded = 2, RuleSet? rules = null)
        => new(Context(rules), state ?? LiveState(), status, previous, freeThrowsAwarded);

    private static GameEvent Event(
        string type, string? subtype = null, int? primary = null, int? secondary = null,
        CompetitionTeamId? team = null, int period = 1, int clock = 300000,
        int? x = null, int? y = null, IReadOnlyDictionary<string, string>? payload = null)
        => GameEvent.Record(
            Guid.CreateVersion7(), OrganisationId.New(),
            GameId.FromGuid(new Guid("00000000-0000-0000-0000-0000000000f1")),
            sequence: 11, eventType: type, eventSubtype: subtype, period: period, gameClockMs: clock,
            recordedAt: DateTimeOffset.UnixEpoch, recordedByUserId: UserId.New(),
            competitionTeamId: team,
            gameRosterEntryId: primary.HasValue ? GoldenFiles.Roster(primary.Value) : null,
            secondaryRosterEntryId: secondary.HasValue ? GoldenFiles.Roster(secondary.Value) : null,
            shotXCm: x, shotYCm: y, payload: payload);

    private static ValidationOutcome Run(ValidationContext context, GameEvent candidate, bool allowOverride = false)
        => new EventValidationPipeline().Evaluate(context, candidate, allowOverride);

    private static void AssertRejected(ValidationOutcome outcome, string code)
    {
        outcome.IsAccepted.Should().BeFalse();
        outcome.Failure!.Code.Should().Be(code);
    }

    // ── Tier 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public void GAME_NOT_IN_PROGRESS()
    {
        Run(Ctx(status: GameStatus.RosterLocked), Event(EventTypes.GameStart)).IsAccepted.Should().BeTrue();
        AssertRejected(Run(Ctx(status: GameStatus.RosterLocked), Event(EventTypes.Turnover, primary: 1)), "GAME_NOT_IN_PROGRESS");
    }

    [Fact]
    public void EVENT_BEFORE_GAME_START()
    {
        Run(Ctx(), Event(EventTypes.Turnover, primary: 1, secondary: 11)).IsAccepted.Should().BeTrue();
        AssertRejected(
            Run(Ctx(LiveState(started: false)), Event(EventTypes.Turnover, primary: 1, secondary: 11)),
            "EVENT_BEFORE_GAME_START");
    }

    [Fact]
    public void PLAYER_NOT_ON_ROSTER()
    {
        Run(Ctx(), Event(EventTypes.Turnover, primary: 1, secondary: 11)).IsAccepted.Should().BeTrue();
        AssertRejected(Run(Ctx(), Event(EventTypes.Turnover, primary: 99)), "PLAYER_NOT_ON_ROSTER");
    }

    [Fact]
    public void WRONG_TEAM()
    {
        Run(Ctx(), Event(EventTypes.Turnover, primary: 1, secondary: 11, team: Home)).IsAccepted.Should().BeTrue();
        AssertRejected(Run(Ctx(), Event(EventTypes.Turnover, primary: 1, secondary: 11, team: Away)), "WRONG_TEAM");
    }

    [Fact]
    public void SHOT_OUT_OF_BOUNDS()
    {
        Run(Ctx(), Event(EventTypes.FieldGoalMade, EventSubtypes.TwoPoint, primary: 1, x: 1150, y: 0))
            .IsAccepted.Should().BeTrue();
        AssertRejected(
            Run(Ctx(), Event(EventTypes.FieldGoalMade, EventSubtypes.TwoPoint, primary: 1, x: 9999, y: 0)),
            "SHOT_OUT_OF_BOUNDS");
    }

    [Fact]
    public void ASSIST_SELF()
    {
        Run(Ctx(), Event(EventTypes.FieldGoalMade, EventSubtypes.TwoPoint, primary: 1, secondary: 2, x: 1150, y: 0))
            .IsAccepted.Should().BeTrue();
        AssertRejected(
            Run(Ctx(), Event(EventTypes.FieldGoalMade, EventSubtypes.TwoPoint, primary: 1, secondary: 1, x: 1150, y: 0)),
            "ASSIST_SELF");
    }

    [Fact]
    public void ASSIST_WRONG_TEAM()
    {
        Run(Ctx(), Event(EventTypes.FieldGoalMade, EventSubtypes.TwoPoint, primary: 1, secondary: 2, x: 1150, y: 0))
            .IsAccepted.Should().BeTrue();
        AssertRejected(
            Run(Ctx(), Event(EventTypes.FieldGoalMade, EventSubtypes.TwoPoint, primary: 1, secondary: 11, x: 1150, y: 0)),
            "ASSIST_WRONG_TEAM");
    }

    [Fact]
    public void STEAL_SAME_TEAM()
    {
        Run(Ctx(), Event(EventTypes.Turnover, primary: 1, secondary: 11)).IsAccepted.Should().BeTrue();
        AssertRejected(Run(Ctx(), Event(EventTypes.Turnover, primary: 1, secondary: 2)), "STEAL_SAME_TEAM");
    }

    [Fact]
    public void BLOCK_SAME_TEAM()
    {
        Run(Ctx(), Event(EventTypes.FieldGoalMissed, EventSubtypes.TwoPoint, primary: 1, secondary: 11, x: 1150, y: 0))
            .IsAccepted.Should().BeTrue();
        AssertRejected(
            Run(Ctx(), Event(EventTypes.FieldGoalMissed, EventSubtypes.TwoPoint, primary: 1, secondary: 2, x: 1150, y: 0)),
            "BLOCK_SAME_TEAM");
    }

    [Fact]
    public void FOUL_DRAWN_SAME_TEAM()
    {
        Run(Ctx(), Event(EventTypes.Foul, EventSubtypes.Personal, primary: 1, secondary: 11)).IsAccepted.Should().BeTrue();
        AssertRejected(Run(Ctx(), Event(EventTypes.Foul, EventSubtypes.Personal, primary: 1, secondary: 2)), "FOUL_DRAWN_SAME_TEAM");
    }

    [Fact]
    public void FREE_THROW_SEQUENCE()
    {
        var good = new Dictionary<string, string> { ["attemptNumber"] = "2", ["totalAttempts"] = "2" };
        Run(Ctx(), Event(EventTypes.FreeThrowMade, primary: 1, payload: good)).IsAccepted.Should().BeTrue();

        var bad = new Dictionary<string, string> { ["attemptNumber"] = "3", ["totalAttempts"] = "2" };
        AssertRejected(Run(Ctx(), Event(EventTypes.FreeThrowMade, primary: 1, payload: bad)), "FREE_THROW_SEQUENCE");
    }

    [Fact]
    public void TIED_AT_GAME_END()
    {
        Run(Ctx(LiveState(homeScore: 10, awayScore: 8)), Event(EventTypes.GameEnd)).IsAccepted.Should().BeTrue();
        AssertRejected(Run(Ctx(LiveState(homeScore: 9, awayScore: 9)), Event(EventTypes.GameEnd)), "TIED_AT_GAME_END");
    }

    [Fact]
    public void A_tied_game_may_end_when_the_rule_set_allows_ties()
    {
        var rules = RuleSet.Fiba() with { AllowsTies = true };
        Run(Ctx(LiveState(homeScore: 9, awayScore: 9), rules: rules), Event(EventTypes.GameEnd))
            .IsAccepted.Should().BeTrue();
    }

    // ── Tier 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public void PLAYER_NOT_ON_COURT()
    {
        Run(Ctx(), Event(EventTypes.Turnover, primary: 1, secondary: 11)).IsAccepted.Should().BeTrue();
        AssertRejected(Run(Ctx(), Event(EventTypes.Turnover, primary: 6, secondary: 11)), "PLAYER_NOT_ON_COURT");
    }

    [Fact]
    public void PLAYER_FOULED_OUT()
    {
        Run(Ctx(), Event(EventTypes.Turnover, primary: 1, secondary: 11)).IsAccepted.Should().BeTrue();

        var state = LiveState(fouledOut: [GoldenFiles.Roster(1)]);
        AssertRejected(Run(Ctx(state), Event(EventTypes.Turnover, primary: 1, secondary: 11)), "PLAYER_FOULED_OUT");
    }

    [Fact]
    public void INVALID_LINEUP_SIZE()
    {
        Run(Ctx(), Event(EventTypes.Substitution, primary: 1, secondary: 6)).IsAccepted.Should().BeTrue();
        AssertRejected(Run(Ctx(), Event(EventTypes.Substitution, primary: 1, secondary: 11)), "INVALID_LINEUP_SIZE");
    }

    [Fact]
    public void SUBSTITUTION_WHILE_LIVE()
    {
        Run(Ctx(), Event(EventTypes.Substitution, primary: 1, secondary: 6)).IsAccepted.Should().BeTrue();
        AssertRejected(
            Run(Ctx(LiveState(clockRunning: true)), Event(EventTypes.Substitution, primary: 1, secondary: 6)),
            "SUBSTITUTION_WHILE_LIVE");
    }

    [Fact]
    public void CLOCK_NOT_MONOTONIC()
    {
        var previous = Event(EventTypes.Turnover, primary: 1, secondary: 11, clock: 300000);
        Run(Ctx(previous: previous), Event(EventTypes.Turnover, primary: 1, secondary: 11, clock: 290000))
            .IsAccepted.Should().BeTrue();
        AssertRejected(
            Run(Ctx(previous: previous), Event(EventTypes.Turnover, primary: 1, secondary: 11, clock: 310000)),
            "CLOCK_NOT_MONOTONIC");
    }

    [Fact]
    public void CLOCK_OUT_OF_RANGE()
    {
        Run(Ctx(), Event(EventTypes.Turnover, primary: 1, secondary: 11, clock: 600000)).IsAccepted.Should().BeTrue();
        AssertRejected(
            Run(Ctx(), Event(EventTypes.Turnover, primary: 1, secondary: 11, clock: 700000)),
            "CLOCK_OUT_OF_RANGE");
    }

    [Fact]
    public void REBOUND_WITHOUT_MISS()
    {
        var miss = Event(EventTypes.FieldGoalMissed, EventSubtypes.TwoPoint, primary: 11, x: 1150, y: 0);
        Run(Ctx(previous: miss), Event(EventTypes.Rebound, EventSubtypes.Defensive, primary: 1))
            .IsAccepted.Should().BeTrue();

        var made = Event(EventTypes.FieldGoalMade, EventSubtypes.TwoPoint, primary: 11, x: 1150, y: 0);
        AssertRejected(
            Run(Ctx(previous: made), Event(EventTypes.Rebound, EventSubtypes.Defensive, primary: 1)),
            "REBOUND_WITHOUT_MISS");
    }

    [Fact]
    public void FREE_THROW_WITHOUT_SOURCE()
    {
        Run(Ctx(freeThrowsAwarded: 2), Event(EventTypes.FreeThrowMade, primary: 1)).IsAccepted.Should().BeTrue();
        AssertRejected(
            Run(Ctx(freeThrowsAwarded: 0), Event(EventTypes.FreeThrowMade, primary: 1)),
            "FREE_THROW_WITHOUT_SOURCE");
    }

    [Fact]
    public void SHOT_ZONE_MISMATCH()
    {
        // A genuine three from beyond the arc.
        Run(Ctx(), Event(EventTypes.FieldGoalMade, EventSubtypes.ThreePoint, primary: 1, x: 500, y: 0))
            .IsAccepted.Should().BeTrue();

        // A "three" tapped at coordinates inside the arc.
        AssertRejected(
            Run(Ctx(), Event(EventTypes.FieldGoalMade, EventSubtypes.ThreePoint, primary: 1, x: 1150, y: 0)),
            "SHOT_ZONE_MISMATCH");
    }

    [Fact]
    public void TIMEOUT_LIMIT_EXCEEDED()
    {
        Run(Ctx(), Event(EventTypes.Timeout, team: Home)).IsAccepted.Should().BeTrue();
        AssertRejected(Run(Ctx(LiveState(timeouts: 0)), Event(EventTypes.Timeout, team: Home)), "TIMEOUT_LIMIT_EXCEEDED");
    }

    [Fact]
    public void PERIOD_NOT_COMPLETE()
    {
        Run(Ctx(), Event(EventTypes.PeriodEnd, clock: 0)).IsAccepted.Should().BeTrue();
        AssertRejected(Run(Ctx(), Event(EventTypes.PeriodEnd, clock: 5000)), "PERIOD_NOT_COMPLETE");
    }

    // ── Override mechanism ───────────────────────────────────────────────────

    [Fact]
    public void A_tier_two_violation_can_be_overridden_and_is_flagged()
    {
        var candidate = Event(EventTypes.FieldGoalMade, EventSubtypes.ThreePoint, primary: 1, x: 1150, y: 0);

        var outcome = Run(Ctx(), candidate, allowOverride: true);

        outcome.IsAccepted.Should().BeTrue();
        outcome.WasOverridden.Should().BeTrue();
        outcome.Failure!.Code.Should().Be("SHOT_ZONE_MISMATCH");
    }

    [Fact]
    public void A_tier_one_violation_can_never_be_overridden()
    {
        var candidate = Event(EventTypes.FieldGoalMade, EventSubtypes.TwoPoint, primary: 1, secondary: 1, x: 1150, y: 0);

        var outcome = Run(Ctx(), candidate, allowOverride: true);

        outcome.IsAccepted.Should().BeFalse("ASSIST_SELF is tier 1");
        outcome.Failure!.Code.Should().Be("ASSIST_SELF");
    }

    [Fact]
    public void Every_documented_rule_code_is_registered_in_the_pipeline()
    {
        string[] documented =
        [
            "GAME_NOT_IN_PROGRESS", "EVENT_BEFORE_GAME_START", "PLAYER_NOT_ON_ROSTER", "WRONG_TEAM",
            "SHOT_OUT_OF_BOUNDS", "ASSIST_SELF", "ASSIST_WRONG_TEAM", "STEAL_SAME_TEAM", "BLOCK_SAME_TEAM",
            "FOUL_DRAWN_SAME_TEAM", "FREE_THROW_SEQUENCE", "TIED_AT_GAME_END",
            "PLAYER_NOT_ON_COURT", "PLAYER_FOULED_OUT", "INVALID_LINEUP_SIZE", "SUBSTITUTION_WHILE_LIVE",
            "CLOCK_NOT_MONOTONIC", "CLOCK_OUT_OF_RANGE", "REBOUND_WITHOUT_MISS", "FREE_THROW_WITHOUT_SOURCE",
            "SHOT_ZONE_MISMATCH", "TIMEOUT_LIMIT_EXCEEDED", "PERIOD_NOT_COMPLETE",
        ];

        new EventValidationPipeline().Codes.Should().BeEquivalentTo(documented);
    }
}
