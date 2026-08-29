using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Competitions.Domain;

/// <summary>A canonical team's entry into one competition — optionally assigned to a group, with a seed.</summary>
public sealed class CompetitionTeam : ITenantScoped, IAuditableEntity
{
    private CompetitionTeam()
    {
    }

    private CompetitionTeam(
        CompetitionTeamId id, OrganisationId organisationId, CompetitionId competitionId, TeamId teamId)
    {
        Id = id;
        OrganisationId = organisationId;
        CompetitionId = competitionId;
        TeamId = teamId;
    }

    /// <summary>The primary key.</summary>
    public CompetitionTeamId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The competition entered.</summary>
    public CompetitionId CompetitionId { get; private set; }

    /// <summary>The canonical team.</summary>
    public TeamId TeamId { get; private set; }

    /// <summary>The group this entry is assigned to, if any.</summary>
    public GroupId? GroupId { get; private set; }

    /// <summary>Per-competition name override (e.g. a sponsored name); null uses the team's name.</summary>
    public string? DisplayName { get; private set; }

    /// <summary>Optional seeding.</summary>
    public int? Seed { get; private set; }

    /// <summary>Entry status.</summary>
    public CompetitionTeamStatus Status { get; private set; } = CompetitionTeamStatus.Registered;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Enters a team into a competition.</summary>
    public static CompetitionTeam Enter(
        OrganisationId organisationId, CompetitionId competitionId, TeamId teamId)
        => new(CompetitionTeamId.New(), organisationId, competitionId, teamId);

    /// <summary>Updates group, seed, display name, and status; omitted arguments leave fields unchanged.</summary>
    public void Update(GroupId? groupId, int? seed, string? displayName, CompetitionTeamStatus? status)
    {
        if (groupId.HasValue)
        {
            GroupId = groupId;
        }

        if (seed.HasValue)
        {
            Seed = seed;
        }

        if (displayName is not null)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        }

        if (status.HasValue)
        {
            Status = status.Value;
        }
    }
}
