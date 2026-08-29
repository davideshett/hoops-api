using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Competitions.Contracts;

/// <summary>Season use cases.</summary>
public interface ISeasonService
{
    /// <summary>Lists an organisation's seasons.</summary>
    Task<Result<IReadOnlyList<SeasonDto>>> ListAsync(OrganisationId organisationId, CancellationToken ct = default);

    /// <summary>Creates a season.</summary>
    Task<Result<SeasonDto>> CreateAsync(OrganisationId organisationId, CreateSeasonRequest request, CancellationToken ct = default);

    /// <summary>Fetches a season.</summary>
    Task<Result<SeasonDto>> GetAsync(SeasonId id, CancellationToken ct = default);

    /// <summary>Updates a season.</summary>
    Task<Result<SeasonDto>> UpdateAsync(SeasonId id, UpdateSeasonRequest request, CancellationToken ct = default);
}

/// <summary>Competition, stage, group, entry, and staff use cases.</summary>
public interface ICompetitionService
{
    /// <summary>Lists competitions, optionally filtered by season and/or status.</summary>
    Task<Result<IReadOnlyList<CompetitionDto>>> ListAsync(
        OrganisationId organisationId, SeasonId? seasonId, string? status, CancellationToken ct = default);

    /// <summary>Creates a competition.</summary>
    Task<Result<CompetitionDto>> CreateAsync(
        OrganisationId organisationId, CreateCompetitionRequest request, CancellationToken ct = default);

    /// <summary>Fetches a competition.</summary>
    Task<Result<CompetitionDto>> GetAsync(CompetitionId id, CancellationToken ct = default);

    /// <summary>Updates a competition's profile.</summary>
    Task<Result<CompetitionDto>> UpdateAsync(CompetitionId id, UpdateCompetitionRequest request, CancellationToken ct = default);

    /// <summary>Moves a competition to a new lifecycle status.</summary>
    Task<Result<CompetitionDto>> ChangeStatusAsync(CompetitionId id, ChangeCompetitionStatusRequest request, CancellationToken ct = default);

    /// <summary>Lists a competition's stages.</summary>
    Task<Result<IReadOnlyList<StageDto>>> ListStagesAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>Adds a stage to a competition.</summary>
    Task<Result<StageDto>> CreateStageAsync(CompetitionId competitionId, CreateStageRequest request, CancellationToken ct = default);

    /// <summary>Lists a stage's groups.</summary>
    Task<Result<IReadOnlyList<GroupDto>>> ListGroupsAsync(StageId stageId, CancellationToken ct = default);

    /// <summary>Adds a group to a stage.</summary>
    Task<Result<GroupDto>> CreateGroupAsync(StageId stageId, CreateGroupRequest request, CancellationToken ct = default);

    /// <summary>Lists a competition's team entries.</summary>
    Task<Result<IReadOnlyList<CompetitionTeamDto>>> ListTeamsAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>Enters an existing canonical team into a competition.</summary>
    Task<Result<CompetitionTeamDto>> EnterTeamAsync(CompetitionId competitionId, EnterTeamRequest request, CancellationToken ct = default);

    /// <summary>Updates a competition entry (group, seed, display name, status).</summary>
    Task<Result<CompetitionTeamDto>> UpdateEntryAsync(CompetitionTeamId id, UpdateCompetitionTeamRequest request, CancellationToken ct = default);

    /// <summary>Lists staff on a competition entry.</summary>
    Task<Result<IReadOnlyList<TeamStaffDto>>> ListStaffAsync(CompetitionTeamId competitionTeamId, CancellationToken ct = default);

    /// <summary>Adds a staff member to a competition entry.</summary>
    Task<Result<TeamStaffDto>> AddStaffAsync(CompetitionTeamId competitionTeamId, AddStaffRequest request, CancellationToken ct = default);
}

/// <summary>Canonical team use cases.</summary>
public interface ITeamService
{
    /// <summary>Lists an organisation's teams, optionally filtered by a name search.</summary>
    Task<Result<IReadOnlyList<TeamDto>>> ListAsync(OrganisationId organisationId, string? search, CancellationToken ct = default);

    /// <summary>Creates a canonical team.</summary>
    Task<Result<TeamDto>> CreateAsync(OrganisationId organisationId, CreateTeamRequest request, CancellationToken ct = default);

    /// <summary>Fetches a team.</summary>
    Task<Result<TeamDto>> GetAsync(TeamId id, CancellationToken ct = default);

    /// <summary>Updates a team.</summary>
    Task<Result<TeamDto>> UpdateAsync(TeamId id, UpdateTeamRequest request, CancellationToken ct = default);
}

/// <summary>Venue use cases.</summary>
public interface IVenueService
{
    /// <summary>Lists an organisation's venues.</summary>
    Task<Result<IReadOnlyList<VenueDto>>> ListAsync(OrganisationId organisationId, CancellationToken ct = default);

    /// <summary>Creates a venue.</summary>
    Task<Result<VenueDto>> CreateAsync(OrganisationId organisationId, CreateVenueRequest request, CancellationToken ct = default);

    /// <summary>Fetches a venue.</summary>
    Task<Result<VenueDto>> GetAsync(VenueId id, CancellationToken ct = default);

    /// <summary>Updates a venue.</summary>
    Task<Result<VenueDto>> UpdateAsync(VenueId id, UpdateVenueRequest request, CancellationToken ct = default);
}
