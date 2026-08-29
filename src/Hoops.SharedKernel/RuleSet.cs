namespace Hoops.SharedKernel;

/// <summary>
/// The rules a competition is played under. Stored as jsonb on the competition and snapshotted onto
/// each game at roster lock (Phase 3), so a mid-competition rule change never rewrites played games.
/// Defaults are standard FIBA. This type is the concrete crystallisation of the architecture doc's
/// forward-referenced "§5.3 rule set"; it lives in SharedKernel because the projector, court geometry,
/// and validation pipeline (later phases) all read it.
/// </summary>
public sealed record RuleSet
{
    /// <summary>Periods in regulation. FIBA: 4.</summary>
    public int NumberOfPeriods { get; init; } = 4;

    /// <summary>Length of a regulation period, in seconds. FIBA: 600 (10 minutes).</summary>
    public int PeriodDurationSeconds { get; init; } = 600;

    /// <summary>Length of an overtime period, in seconds. FIBA: 300 (5 minutes).</summary>
    public int OvertimeDurationSeconds { get; init; } = 300;

    /// <summary>Players per team on court. FIBA: 5.</summary>
    public int PlayersOnCourt { get; init; } = 5;

    /// <summary>Minimum active players required to lock a roster. FIBA: 5.</summary>
    public int MinRosterSize { get; init; } = 5;

    /// <summary>Maximum players on a roster. FIBA: 12.</summary>
    public int MaxRosterSize { get; init; } = 12;

    /// <summary>Personal fouls that foul a player out. FIBA: 5.</summary>
    public int PersonalFoulLimit { get; init; } = 5;

    /// <summary>Technical fouls that disqualify a player. FIBA: 2.</summary>
    public int TechnicalFoulLimit { get; init; } = 2;

    /// <summary>Team fouls in a period after which the opponent shoots bonus free throws. FIBA: 5.</summary>
    public int TeamFoulsUntilBonus { get; init; } = 5;

    /// <summary>Team timeouts available in the first half. FIBA: 2.</summary>
    public int TimeoutsFirstHalf { get; init; } = 2;

    /// <summary>Team timeouts available in the second half. FIBA: 3.</summary>
    public int TimeoutsSecondHalf { get; init; } = 3;

    /// <summary>Team timeouts available per overtime period. FIBA: 1.</summary>
    public int TimeoutsPerOvertime { get; init; } = 1;

    /// <summary>Shot-clock length, in seconds. FIBA: 24.</summary>
    public int ShotClockSeconds { get; init; } = 24;

    /// <summary>Shot-clock reset after an offensive rebound, in seconds. FIBA: 14.</summary>
    public int ShotClockResetSeconds { get; init; } = 14;

    /// <summary>Whether a game may end tied (no overtime). FIBA competition: false.</summary>
    public bool AllowsTies { get; init; }

    /// <summary>Minimum identity tier a player must hold to be rostered (§5A.2). Default 0 (open age).</summary>
    public int MinimumIdentityTier { get; init; }

    /// <summary>The standard FIBA rule set.</summary>
    public static RuleSet Fiba() => new();
}
