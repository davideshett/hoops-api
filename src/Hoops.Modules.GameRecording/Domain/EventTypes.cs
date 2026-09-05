namespace Hoops.Modules.GameRecording.Domain;

/// <summary>
/// The event catalogue (§7). Stored as text. Assists, steals, and blocks are deliberately NOT event
/// types — they are the secondary participant on a made basket, a turnover, and a missed shot
/// respectively (ADR-002), which makes an orphaned assist structurally impossible.
/// </summary>
public static class EventTypes
{
    /// <summary>Game start; reads the starting fives from the game roster.</summary>
    public const string GameStart = "GAME_START";

    /// <summary>Period start. Subtype: Regulation | Overtime.</summary>
    public const string PeriodStart = "PERIOD_START";

    /// <summary>Period end; resets team fouls.</summary>
    public const string PeriodEnd = "PERIOD_END";

    /// <summary>Game end.</summary>
    public const string GameEnd = "GAME_END";

    /// <summary>Jump ball.</summary>
    public const string JumpBall = "JUMP_BALL";

    /// <summary>Made field goal. Subtype: TwoPoint | ThreePoint. Secondary = assister.</summary>
    public const string FieldGoalMade = "FIELD_GOAL_MADE";

    /// <summary>Missed field goal. Subtype: TwoPoint | ThreePoint. Secondary = blocker.</summary>
    public const string FieldGoalMissed = "FIELD_GOAL_MISSED";

    /// <summary>Made free throw.</summary>
    public const string FreeThrowMade = "FREE_THROW_MADE";

    /// <summary>Missed free throw.</summary>
    public const string FreeThrowMissed = "FREE_THROW_MISSED";

    /// <summary>Rebound. Subtype: Offensive | Defensive.</summary>
    public const string Rebound = "REBOUND";

    /// <summary>Team rebound. Subtype: Offensive | Defensive | DeadBall.</summary>
    public const string TeamRebound = "TEAM_REBOUND";

    /// <summary>Turnover. Secondary = stealer.</summary>
    public const string Turnover = "TURNOVER";

    /// <summary>Foul. Subtype per §7. Secondary = player fouled.</summary>
    public const string Foul = "FOUL";

    /// <summary>Substitution: player out is primary, player in is secondary.</summary>
    public const string Substitution = "SUBSTITUTION";

    /// <summary>Timeout.</summary>
    public const string Timeout = "TIMEOUT";

    /// <summary>Clock start.</summary>
    public const string ClockStart = "CLOCK_START";

    /// <summary>Clock stop.</summary>
    public const string ClockStop = "CLOCK_STOP";

    /// <summary>Voids a target event. Payload carries targetEventId and reason.</summary>
    public const string Void = "VOID";

    /// <summary>Free-text annotation. Ignored by the projector.</summary>
    public const string Note = "NOTE";

    /// <summary>Every recognised event type.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        GameStart, PeriodStart, PeriodEnd, GameEnd, JumpBall,
        FieldGoalMade, FieldGoalMissed, FreeThrowMade, FreeThrowMissed,
        Rebound, TeamRebound, Turnover, Foul, Substitution, Timeout,
        ClockStart, ClockStop, Void, Note,
    };
}

/// <summary>Event subtypes (§7). Stored as text.</summary>
public static class EventSubtypes
{
    /// <summary>A two-point attempt.</summary>
    public const string TwoPoint = "TwoPoint";

    /// <summary>A three-point attempt.</summary>
    public const string ThreePoint = "ThreePoint";

    /// <summary>A regulation period.</summary>
    public const string Regulation = "Regulation";

    /// <summary>An overtime period.</summary>
    public const string Overtime = "Overtime";

    /// <summary>An offensive rebound.</summary>
    public const string Offensive = "Offensive";

    /// <summary>A defensive rebound.</summary>
    public const string Defensive = "Defensive";

    /// <summary>A dead-ball team rebound.</summary>
    public const string DeadBall = "DeadBall";

    // ── Foul subtypes ────────────────────────────────────────────────────────

    /// <summary>Personal foul.</summary>
    public const string Personal = "Personal";

    /// <summary>Shooting foul.</summary>
    public const string Shooting = "Shooting";

    /// <summary>Offensive foul.</summary>
    public const string OffensiveFoul = "Offensive";

    /// <summary>Technical foul on a player.</summary>
    public const string Technical = "Technical";

    /// <summary>Unsportsmanlike foul.</summary>
    public const string Unsportsmanlike = "Unsportsmanlike";

    /// <summary>Disqualifying foul.</summary>
    public const string Disqualifying = "Disqualifying";

    /// <summary>Bench technical (team fouls only).</summary>
    public const string BenchTechnical = "BenchTechnical";

    /// <summary>Coach technical (team fouls only).</summary>
    public const string CoachTechnical = "CoachTechnical";

    /// <summary>Foul subtypes that count against the offending player AND the team.</summary>
    public static readonly IReadOnlySet<string> PlayerAndTeamFouls = new HashSet<string>(StringComparer.Ordinal)
    {
        Personal, Shooting, OffensiveFoul, Unsportsmanlike, Disqualifying, Technical,
    };

    /// <summary>Foul subtypes charged to the bench, not to a player.</summary>
    public static readonly IReadOnlySet<string> BenchFouls = new HashSet<string>(StringComparer.Ordinal)
    {
        BenchTechnical, CoachTechnical,
    };
}
