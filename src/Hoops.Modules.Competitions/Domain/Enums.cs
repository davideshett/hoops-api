namespace Hoops.Modules.Competitions.Domain;

/// <summary>The competitive format of a competition. Stored as text.</summary>
public enum CompetitionFormat
{
    /// <summary>Every team plays a table; ranked by points.</summary>
    League = 0,

    /// <summary>Single- or double-elimination bracket.</summary>
    Knockout = 1,

    /// <summary>Group stage feeding a knockout bracket.</summary>
    GroupsThenKnockout = 2,

    /// <summary>Every team plays every other team.</summary>
    RoundRobin = 3,
}

/// <summary>Lifecycle status of a competition. Stored as text.</summary>
public enum CompetitionStatus
{
    /// <summary>Being set up; not yet open.</summary>
    Draft = 0,

    /// <summary>Open for team registration.</summary>
    Registration = 1,

    /// <summary>Under way.</summary>
    InProgress = 2,

    /// <summary>Finished.</summary>
    Completed = 3,

    /// <summary>Read-only historical record.</summary>
    Archived = 4,
}

/// <summary>The kind of a stage within a competition. Stored as text.</summary>
public enum StageType
{
    /// <summary>Round-robin pools.</summary>
    GroupStage = 0,

    /// <summary>Elimination bracket.</summary>
    Knockout = 1,

    /// <summary>Placement / classification matches.</summary>
    Placement = 2,
}

/// <summary>A team's status within one competition. Stored as text.</summary>
public enum CompetitionTeamStatus
{
    /// <summary>Entry submitted.</summary>
    Registered = 0,

    /// <summary>Entry confirmed by the organiser.</summary>
    Confirmed = 1,

    /// <summary>Pulled out.</summary>
    Withdrawn = 2,

    /// <summary>Removed by the organiser.</summary>
    Disqualified = 3,
}

/// <summary>A staff member's role on a team. Stored as text.</summary>
public enum TeamStaffRole
{
    /// <summary>Head coach.</summary>
    HeadCoach = 0,

    /// <summary>Assistant coach.</summary>
    AssistantCoach = 1,

    /// <summary>Team manager.</summary>
    Manager = 2,
}
