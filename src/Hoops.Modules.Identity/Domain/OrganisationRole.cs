namespace Hoops.Modules.Identity.Domain;

/// <summary>
/// A member's role within one organisation. Stored as text in the database (never an ordinal) so the
/// schema stays readable and reorderable. Ordered loosely from most to least privileged.
/// </summary>
public enum OrganisationRole
{
    /// <summary>Full control including deletion and ownership transfer. The creator of an org.</summary>
    Owner = 0,

    /// <summary>Administrative control: members, settings, competitions.</summary>
    Admin = 1,

    /// <summary>Manages competitions, teams, rosters, and finalises games.</summary>
    CompetitionManager = 2,

    /// <summary>Records live game events at the scorer's table.</summary>
    Statistician = 3,

    /// <summary>Read-only access.</summary>
    Viewer = 4,
}
