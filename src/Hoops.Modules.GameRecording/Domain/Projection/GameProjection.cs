using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.GameRecording.Domain.Projection;

/// <summary>
/// One player's derived statline. Percentages are null when the denominator is zero (§9.2) — a player
/// who took no threes has no three-point percentage, and rendering 0% would be wrong.
/// </summary>
public sealed record PlayerStatline
{
    /// <summary>The game-roster entry this line belongs to.</summary>
    public required GameRosterEntryId GameRosterEntryId { get; init; }

    /// <summary>The team.</summary>
    public required CompetitionTeamId CompetitionTeamId { get; init; }

    /// <summary>The registry player.</summary>
    public required PlayerId PlayerId { get; init; }

    /// <summary>Total points.</summary>
    public int Points { get; init; }

    /// <summary>Field goals made (includes threes).</summary>
    public int FieldGoalsMade { get; init; }

    /// <summary>Field goals attempted (includes threes).</summary>
    public int FieldGoalsAttempted { get; init; }

    /// <summary>Three-pointers made — a SUBSET of field goals, not additive.</summary>
    public int ThreePointersMade { get; init; }

    /// <summary>Three-pointers attempted — a SUBSET of field-goal attempts.</summary>
    public int ThreePointersAttempted { get; init; }

    /// <summary>Free throws made.</summary>
    public int FreeThrowsMade { get; init; }

    /// <summary>Free throws attempted.</summary>
    public int FreeThrowsAttempted { get; init; }

    /// <summary>Offensive rebounds.</summary>
    public int OffensiveRebounds { get; init; }

    /// <summary>Defensive rebounds.</summary>
    public int DefensiveRebounds { get; init; }

    /// <summary>Assists.</summary>
    public int Assists { get; init; }

    /// <summary>Steals.</summary>
    public int Steals { get; init; }

    /// <summary>Blocks.</summary>
    public int Blocks { get; init; }

    /// <summary>Times this player's shot was blocked.</summary>
    public int BlocksAgainst { get; init; }

    /// <summary>Turnovers committed.</summary>
    public int Turnovers { get; init; }

    /// <summary>Personal fouls committed (includes player technicals).</summary>
    public int FoulsCommitted { get; init; }

    /// <summary>Fouls drawn.</summary>
    public int FoulsDrawn { get; init; }

    /// <summary>Whether the player reached the personal-foul limit.</summary>
    public bool FouledOut { get; init; }

    /// <summary>Seconds played, accumulated while the clock ran and the player was on court.</summary>
    public int SecondsPlayed { get; init; }

    /// <summary>Team points minus opponent points while this player was on court.</summary>
    public int PlusMinus { get; init; }

    /// <summary>Total rebounds.</summary>
    public int TotalRebounds => OffensiveRebounds + DefensiveRebounds;

    /// <summary>Field-goal percentage, or null when no attempts.</summary>
    public double? FieldGoalPercentage => Pct(FieldGoalsMade, FieldGoalsAttempted);

    /// <summary>Three-point percentage, or null when no attempts.</summary>
    public double? ThreePointPercentage => Pct(ThreePointersMade, ThreePointersAttempted);

    /// <summary>Free-throw percentage, or null when no attempts.</summary>
    public double? FreeThrowPercentage => Pct(FreeThrowsMade, FreeThrowsAttempted);

    /// <summary>Effective field-goal percentage, or null when no attempts.</summary>
    public double? EffectiveFieldGoalPercentage
        => FieldGoalsAttempted == 0 ? null : (FieldGoalsMade + (0.5 * ThreePointersMade)) / FieldGoalsAttempted;

    /// <summary>True shooting percentage, or null when there were no shooting possessions.</summary>
    public double? TrueShootingPercentage
    {
        get
        {
            var denominator = 2 * (FieldGoalsAttempted + (0.44 * FreeThrowsAttempted));
            return denominator == 0 ? null : Points / denominator;
        }
    }

    /// <summary>FIBA efficiency (§9.2).</summary>
    public int Efficiency
        => Points + TotalRebounds + Assists + Steals + Blocks + FoulsDrawn
           - (FieldGoalsAttempted - FieldGoalsMade)
           - (FreeThrowsAttempted - FreeThrowsMade)
           - Turnovers - BlocksAgainst - FoulsCommitted;

    private static double? Pct(int made, int attempted) => attempted == 0 ? null : (double)made / attempted;
}

/// <summary>One team's derived statline. Team points always equal the sum of its players' points.</summary>
public sealed record TeamStatline
{
    /// <summary>The team.</summary>
    public required CompetitionTeamId CompetitionTeamId { get; init; }

    /// <summary>Total points.</summary>
    public int Points { get; init; }

    /// <summary>Field goals made.</summary>
    public int FieldGoalsMade { get; init; }

    /// <summary>Field goals attempted.</summary>
    public int FieldGoalsAttempted { get; init; }

    /// <summary>Three-pointers made.</summary>
    public int ThreePointersMade { get; init; }

    /// <summary>Three-pointers attempted.</summary>
    public int ThreePointersAttempted { get; init; }

    /// <summary>Free throws made.</summary>
    public int FreeThrowsMade { get; init; }

    /// <summary>Free throws attempted.</summary>
    public int FreeThrowsAttempted { get; init; }

    /// <summary>Offensive rebounds, including team rebounds.</summary>
    public int OffensiveRebounds { get; init; }

    /// <summary>Defensive rebounds, including team rebounds.</summary>
    public int DefensiveRebounds { get; init; }

    /// <summary>Assists.</summary>
    public int Assists { get; init; }

    /// <summary>Steals.</summary>
    public int Steals { get; init; }

    /// <summary>Blocks.</summary>
    public int Blocks { get; init; }

    /// <summary>Turnovers.</summary>
    public int Turnovers { get; init; }

    /// <summary>Fouls charged to the team (includes bench and coach technicals).</summary>
    public int FoulsCommitted { get; init; }

    /// <summary>Timeouts taken.</summary>
    public int TimeoutsTaken { get; init; }

    /// <summary>Total rebounds.</summary>
    public int TotalRebounds => OffensiveRebounds + DefensiveRebounds;
}

/// <summary>Per-period scoring and team-foul state.</summary>
public sealed record PeriodState
{
    /// <summary>The period number (1-based).</summary>
    public required int Period { get; init; }

    /// <summary>Whether the period has ended.</summary>
    public bool IsComplete { get; init; }

    /// <summary>Points scored in this period, by team.</summary>
    public required IReadOnlyDictionary<CompetitionTeamId, int> PointsByTeam { get; init; }

    /// <summary>Team fouls committed in this period, by team (reset each period).</summary>
    public required IReadOnlyDictionary<CompetitionTeamId, int> TeamFoulsByTeam { get; init; }
}

/// <summary>
/// The complete derived state of a game: statlines, period states, running score, and the live state
/// the recording app renders. Produced only by the projector, never authored.
/// </summary>
public sealed record GameProjection
{
    /// <summary>Player statlines, keyed by game-roster entry.</summary>
    public required IReadOnlyList<PlayerStatline> PlayerStatlines { get; init; }

    /// <summary>Team statlines.</summary>
    public required IReadOnlyList<TeamStatline> TeamStatlines { get; init; }

    /// <summary>Per-period state.</summary>
    public required IReadOnlyList<PeriodState> Periods { get; init; }

    /// <summary>Running score by team.</summary>
    public required IReadOnlyDictionary<CompetitionTeamId, int> Score { get; init; }

    /// <summary>The live state for the recording app.</summary>
    public required LiveGameState LiveState { get; init; }
}

/// <summary>The recording app's home screen — everything it needs to render, derived from the log.</summary>
public sealed record LiveGameState
{
    /// <summary>The game.</summary>
    public required GameId GameId { get; init; }

    /// <summary>The highest sequence included in this state.</summary>
    public required long LastSequence { get; init; }

    /// <summary>The current period.</summary>
    public int CurrentPeriod { get; init; }

    /// <summary>Milliseconds remaining on the game clock.</summary>
    public int GameClockMs { get; init; }

    /// <summary>Whether the clock is running.</summary>
    public bool ClockRunning { get; init; }

    /// <summary>Whether the game has started.</summary>
    public bool GameStarted { get; init; }

    /// <summary>Whether the game has ended.</summary>
    public bool GameEnded { get; init; }

    /// <summary>Whether the current period has ended.</summary>
    public bool PeriodEnded { get; init; }

    /// <summary>Running score by team.</summary>
    public required IReadOnlyDictionary<CompetitionTeamId, int> Score { get; init; }

    /// <summary>Team fouls in the current period, by team.</summary>
    public required IReadOnlyDictionary<CompetitionTeamId, int> TeamFouls { get; init; }

    /// <summary>Timeouts remaining, by team.</summary>
    public required IReadOnlyDictionary<CompetitionTeamId, int> TimeoutsRemaining { get; init; }

    /// <summary>Players currently on court, by team.</summary>
    public required IReadOnlyDictionary<CompetitionTeamId, IReadOnlyList<GameRosterEntryId>> OnCourt { get; init; }

    /// <summary>Players who have fouled out.</summary>
    public required IReadOnlyList<GameRosterEntryId> FouledOut { get; init; }
}
