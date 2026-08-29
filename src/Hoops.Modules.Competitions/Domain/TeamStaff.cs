using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Competitions.Domain;

/// <summary>
/// A staff member attached to a team's entry in a competition. Required later for bench and coach
/// technical fouls (§7); modelled here so rosters can be assembled with their coaching staff.
/// </summary>
public sealed class TeamStaff : ITenantScoped, IAuditableEntity
{
    private TeamStaff()
    {
        FullName = null!;
    }

    private TeamStaff(
        TeamStaffId id, OrganisationId organisationId, CompetitionTeamId competitionTeamId,
        string fullName, TeamStaffRole role)
    {
        Id = id;
        OrganisationId = organisationId;
        CompetitionTeamId = competitionTeamId;
        FullName = fullName;
        Role = role;
    }

    /// <summary>The primary key.</summary>
    public TeamStaffId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The competition-team entry this staff member belongs to.</summary>
    public CompetitionTeamId CompetitionTeamId { get; private set; }

    /// <summary>Full name.</summary>
    public string FullName { get; private set; }

    /// <summary>Role on the team.</summary>
    public TeamStaffRole Role { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Adds a staff member.</summary>
    public static TeamStaff Create(
        OrganisationId organisationId, CompetitionTeamId competitionTeamId, string fullName, TeamStaffRole role)
    {
        Guard.AgainstNullOrWhiteSpace(fullName);
        return new TeamStaff(TeamStaffId.New(), organisationId, competitionTeamId, fullName.Trim(), role);
    }

    /// <summary>Updates mutable fields; omitted arguments leave fields unchanged.</summary>
    public void Update(string? fullName, TeamStaffRole? role)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            FullName = fullName.Trim();
        }

        if (role.HasValue)
        {
            Role = role.Value;
        }
    }
}
