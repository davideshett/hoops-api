namespace Hoops.Modules.GameRecording.Domain;

/// <summary>
/// The game state machine (§6). Phase 3 implements Scheduled → RosterLocked plus the terminal-ish
/// Postponed / Cancelled branches; the InProgress → Finalized path arrives in Phase 4.
/// </summary>
public enum GameStatus
{
    /// <summary>Created, not yet locked.</summary>
    Scheduled = 0,

    /// <summary>Rosters and rule set snapshotted; ready to start.</summary>
    RosterLocked = 1,

    /// <summary>Under way (Phase 4).</summary>
    InProgress = 2,

    /// <summary>Awaiting review (Phase 4).</summary>
    PendingReview = 3,

    /// <summary>Final (Phase 4).</summary>
    Finalized = 4,

    /// <summary>Postponed; can be rescheduled.</summary>
    Postponed = 5,

    /// <summary>Cancelled.</summary>
    Cancelled = 6,

    /// <summary>Forfeited (Phase 5).</summary>
    Forfeited = 7,

    /// <summary>Reopened for correction (Phase 5).</summary>
    Amending = 8,
}

/// <summary>An official's role at the scorer's table. Stored as text.</summary>
public enum OfficialRole
{
    /// <summary>Crew chief / lead referee.</summary>
    Referee = 0,

    /// <summary>Umpire.</summary>
    Umpire = 1,

    /// <summary>Scorer.</summary>
    Scorer = 2,

    /// <summary>Timekeeper.</summary>
    Timekeeper = 3,

    /// <summary>Shot-clock operator.</summary>
    ShotClockOperator = 4,

    /// <summary>Commissioner.</summary>
    Commissioner = 5,
}
