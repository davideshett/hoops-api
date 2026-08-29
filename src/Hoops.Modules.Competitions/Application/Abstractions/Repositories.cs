using Hoops.Modules.Competitions.Domain;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Competitions.Application.Abstractions;

/// <summary>
/// Persistence for the competitions module. All reads are automatically constrained to the current
/// tenant by <c>AppDbContext</c>'s global query filter, so these methods never leak across
/// organisations. Declared here, implemented in Infrastructure.
/// </summary>
public interface ISeasonRepository
{
    /// <summary>Finds a season within the current tenant, or null.</summary>
    Task<Season?> GetAsync(SeasonId id, CancellationToken ct = default);

    /// <summary>Lists the organisation's seasons.</summary>
    Task<IReadOnlyList<Season>> ListAsync(OrganisationId organisationId, CancellationToken ct = default);

    /// <summary>True if a season with this name already exists in the organisation.</summary>
    Task<bool> ExistsByNameAsync(OrganisationId organisationId, string name, CancellationToken ct = default);

    /// <summary>Stages a new season for insertion.</summary>
    void Add(Season season);
}

/// <summary>Persistence for competitions.</summary>
public interface ICompetitionRepository
{
    /// <summary>Finds a competition within the current tenant, or null.</summary>
    Task<Competition?> GetAsync(CompetitionId id, CancellationToken ct = default);

    /// <summary>Lists competitions, optionally filtered by season and/or status.</summary>
    Task<IReadOnlyList<Competition>> ListAsync(
        OrganisationId organisationId, SeasonId? seasonId, CompetitionStatus? status, CancellationToken ct = default);

    /// <summary>True if a competition with this slug exists in the (organisation, season).</summary>
    Task<bool> ExistsBySlugAsync(OrganisationId organisationId, SeasonId seasonId, string slug, CancellationToken ct = default);

    /// <summary>Stages a new competition for insertion.</summary>
    void Add(Competition competition);
}

/// <summary>Persistence for stages.</summary>
public interface IStageRepository
{
    /// <summary>Finds a stage within the current tenant, or null.</summary>
    Task<Stage?> GetAsync(StageId id, CancellationToken ct = default);

    /// <summary>Lists a competition's stages, ordered by sequence.</summary>
    Task<IReadOnlyList<Stage>> ListForCompetitionAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>True if a stage with this sequence exists in the competition.</summary>
    Task<bool> ExistsBySequenceAsync(CompetitionId competitionId, int sequence, CancellationToken ct = default);

    /// <summary>Stages a new stage for insertion.</summary>
    void Add(Stage stage);
}

/// <summary>Persistence for groups.</summary>
public interface IGroupRepository
{
    /// <summary>Lists a stage's groups.</summary>
    Task<IReadOnlyList<Group>> ListForStageAsync(StageId stageId, CancellationToken ct = default);

    /// <summary>Stages a new group for insertion.</summary>
    void Add(Group group);
}

/// <summary>Persistence for canonical teams.</summary>
public interface ITeamRepository
{
    /// <summary>Finds a team within the current tenant, or null.</summary>
    Task<Team?> GetAsync(TeamId id, CancellationToken ct = default);

    /// <summary>Lists the organisation's teams, optionally filtered by a name search.</summary>
    Task<IReadOnlyList<Team>> ListAsync(OrganisationId organisationId, string? search, CancellationToken ct = default);

    /// <summary>Stages a new team for insertion.</summary>
    void Add(Team team);
}

/// <summary>Persistence for competition entries.</summary>
public interface ICompetitionTeamRepository
{
    /// <summary>Finds a competition entry within the current tenant, or null.</summary>
    Task<CompetitionTeam?> GetAsync(CompetitionTeamId id, CancellationToken ct = default);

    /// <summary>Lists a competition's team entries.</summary>
    Task<IReadOnlyList<CompetitionTeam>> ListForCompetitionAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>True if the team is already entered in the competition.</summary>
    Task<bool> ExistsAsync(CompetitionId competitionId, TeamId teamId, CancellationToken ct = default);

    /// <summary>Stages a new competition entry for insertion.</summary>
    void Add(CompetitionTeam competitionTeam);
}

/// <summary>Persistence for team staff.</summary>
public interface ITeamStaffRepository
{
    /// <summary>Lists staff on a competition entry.</summary>
    Task<IReadOnlyList<TeamStaff>> ListForCompetitionTeamAsync(CompetitionTeamId competitionTeamId, CancellationToken ct = default);

    /// <summary>Stages a new staff member for insertion.</summary>
    void Add(TeamStaff staff);
}

/// <summary>Persistence for venues.</summary>
public interface IVenueRepository
{
    /// <summary>Finds a venue within the current tenant, or null.</summary>
    Task<Venue?> GetAsync(VenueId id, CancellationToken ct = default);

    /// <summary>Lists the organisation's venues.</summary>
    Task<IReadOnlyList<Venue>> ListAsync(OrganisationId organisationId, CancellationToken ct = default);

    /// <summary>Stages a new venue for insertion.</summary>
    void Add(Venue venue);
}

/// <summary>Commits staged changes for the competitions module.</summary>
public interface ICompetitionsUnitOfWork
{
    /// <summary>Persists all staged changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
