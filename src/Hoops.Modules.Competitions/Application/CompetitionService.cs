using Hoops.Modules.Competitions.Application.Abstractions;
using Hoops.Modules.Competitions.Contracts;
using Hoops.Modules.Competitions.Domain;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Competitions.Application;

/// <summary>Competition, stage, group, entry, and staff use cases.</summary>
public sealed class CompetitionService : ICompetitionService
{
    private readonly ICompetitionRepository _competitions;
    private readonly ISeasonRepository _seasons;
    private readonly IStageRepository _stages;
    private readonly IGroupRepository _groups;
    private readonly ITeamRepository _teams;
    private readonly ICompetitionTeamRepository _entries;
    private readonly ITeamStaffRepository _staff;
    private readonly ICompetitionsUnitOfWork _unitOfWork;

    /// <summary>Creates the service.</summary>
    public CompetitionService(
        ICompetitionRepository competitions,
        ISeasonRepository seasons,
        IStageRepository stages,
        IGroupRepository groups,
        ITeamRepository teams,
        ICompetitionTeamRepository entries,
        ITeamStaffRepository staff,
        ICompetitionsUnitOfWork unitOfWork)
    {
        _competitions = competitions;
        _seasons = seasons;
        _stages = stages;
        _groups = groups;
        _teams = teams;
        _entries = entries;
        _staff = staff;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CompetitionDto>>> ListAsync(
        OrganisationId organisationId, SeasonId? seasonId, string? status, CancellationToken ct = default)
    {
        CompetitionStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParse<CompetitionStatus>(status, out var parsed))
            {
                return Error.Validation("INVALID_STATUS", $"'{status}' is not a valid competition status.");
            }

            statusFilter = parsed;
        }

        var competitions = await _competitions.ListAsync(organisationId, seasonId, statusFilter, ct);
        return Result.Success<IReadOnlyList<CompetitionDto>>(competitions.Select(c => c.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<CompetitionDto>> CreateAsync(
        OrganisationId organisationId, CreateCompetitionRequest request, CancellationToken ct = default)
    {
        if (!TryParse<CompetitionFormat>(request.Format, out var format))
        {
            return Error.Validation("INVALID_FORMAT", $"'{request.Format}' is not a valid competition format.");
        }

        if (await _seasons.GetAsync(request.SeasonId, ct) is null)
        {
            return Error.Validation("SEASON_NOT_FOUND", "The referenced season does not exist.");
        }

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await _competitions.ExistsBySlugAsync(organisationId, request.SeasonId, slug, ct))
        {
            return Error.Conflict("SLUG_ALREADY_TAKEN", "A competition with this slug already exists in the season.");
        }

        Competition competition;
        try
        {
            competition = Competition.Create(
                organisationId, request.SeasonId, request.Name, slug, format,
                request.Category, request.RuleSet, request.Timezone);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("INVALID_COMPETITION", ex.Message);
        }

        competition.UpdateProfile(null, null, null, null, request.StartsOn, request.EndsOn, null);
        _competitions.Add(competition);
        await _unitOfWork.SaveChangesAsync(ct);
        return competition.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<CompetitionDto>> GetAsync(CompetitionId id, CancellationToken ct = default)
    {
        var competition = await _competitions.GetAsync(id, ct);
        return competition is null ? CompetitionNotFound() : competition.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<CompetitionDto>> UpdateAsync(CompetitionId id, UpdateCompetitionRequest request, CancellationToken ct = default)
    {
        var competition = await _competitions.GetAsync(id, ct);
        if (competition is null)
        {
            return CompetitionNotFound();
        }

        CompetitionFormat? format = null;
        if (!string.IsNullOrWhiteSpace(request.Format))
        {
            if (!TryParse<CompetitionFormat>(request.Format, out var parsed))
            {
                return Error.Validation("INVALID_FORMAT", $"'{request.Format}' is not a valid competition format.");
            }

            format = parsed;
        }

        competition.UpdateProfile(request.Name, request.Category, format, request.RuleSet, request.StartsOn, request.EndsOn, request.Timezone);
        await _unitOfWork.SaveChangesAsync(ct);
        return competition.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<CompetitionDto>> ChangeStatusAsync(CompetitionId id, ChangeCompetitionStatusRequest request, CancellationToken ct = default)
    {
        var competition = await _competitions.GetAsync(id, ct);
        if (competition is null)
        {
            return CompetitionNotFound();
        }

        if (!TryParse<CompetitionStatus>(request.Status, out var target))
        {
            return Error.Validation("INVALID_STATUS", $"'{request.Status}' is not a valid competition status.");
        }

        if (!competition.TransitionTo(target))
        {
            return Error.Conflict("INVALID_STATUS_TRANSITION",
                $"A competition cannot move from {competition.Status} to {target}.");
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return competition.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<StageDto>>> ListStagesAsync(CompetitionId competitionId, CancellationToken ct = default)
    {
        var stages = await _stages.ListForCompetitionAsync(competitionId, ct);
        return Result.Success<IReadOnlyList<StageDto>>(stages.Select(s => s.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<StageDto>> CreateStageAsync(CompetitionId competitionId, CreateStageRequest request, CancellationToken ct = default)
    {
        var competition = await _competitions.GetAsync(competitionId, ct);
        if (competition is null)
        {
            return CompetitionNotFound();
        }

        if (!TryParse<StageType>(request.StageType, out var stageType))
        {
            return Error.Validation("INVALID_STAGE_TYPE", $"'{request.StageType}' is not a valid stage type.");
        }

        if (await _stages.ExistsBySequenceAsync(competitionId, request.Sequence, ct))
        {
            return Error.Conflict("STAGE_SEQUENCE_TAKEN", "A stage with this sequence already exists.");
        }

        Stage stage;
        try
        {
            stage = Stage.Create(competition.OrganisationId, competitionId, request.Name, stageType, request.Sequence);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("INVALID_STAGE", ex.Message);
        }

        _stages.Add(stage);
        await _unitOfWork.SaveChangesAsync(ct);
        return stage.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<GroupDto>>> ListGroupsAsync(StageId stageId, CancellationToken ct = default)
    {
        var groups = await _groups.ListForStageAsync(stageId, ct);
        return Result.Success<IReadOnlyList<GroupDto>>(groups.Select(g => g.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<GroupDto>> CreateGroupAsync(StageId stageId, CreateGroupRequest request, CancellationToken ct = default)
    {
        var stage = await _stages.GetAsync(stageId, ct);
        if (stage is null)
        {
            return Error.NotFound("STAGE_NOT_FOUND", "The stage does not exist.");
        }

        var group = Group.Create(stage.OrganisationId, stageId, request.Name);
        _groups.Add(group);
        await _unitOfWork.SaveChangesAsync(ct);
        return group.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CompetitionTeamDto>>> ListTeamsAsync(CompetitionId competitionId, CancellationToken ct = default)
    {
        var entries = await _entries.ListForCompetitionAsync(competitionId, ct);
        return Result.Success<IReadOnlyList<CompetitionTeamDto>>(entries.Select(e => e.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<CompetitionTeamDto>> EnterTeamAsync(CompetitionId competitionId, EnterTeamRequest request, CancellationToken ct = default)
    {
        var competition = await _competitions.GetAsync(competitionId, ct);
        if (competition is null)
        {
            return CompetitionNotFound();
        }

        if (await _teams.GetAsync(request.TeamId, ct) is null)
        {
            return Error.Validation("TEAM_NOT_FOUND", "The referenced team does not exist.");
        }

        if (await _entries.ExistsAsync(competitionId, request.TeamId, ct))
        {
            return Error.Conflict("TEAM_ALREADY_ENTERED", "That team is already entered in this competition.");
        }

        var entry = CompetitionTeam.Enter(competition.OrganisationId, competitionId, request.TeamId);
        _entries.Add(entry);
        await _unitOfWork.SaveChangesAsync(ct);
        return entry.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<CompetitionTeamDto>> UpdateEntryAsync(CompetitionTeamId id, UpdateCompetitionTeamRequest request, CancellationToken ct = default)
    {
        var entry = await _entries.GetAsync(id, ct);
        if (entry is null)
        {
            return Error.NotFound("COMPETITION_TEAM_NOT_FOUND", "The competition entry does not exist.");
        }

        CompetitionTeamStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!TryParse<CompetitionTeamStatus>(request.Status, out var parsed))
            {
                return Error.Validation("INVALID_STATUS", $"'{request.Status}' is not a valid entry status.");
            }

            status = parsed;
        }

        entry.Update(request.GroupId, request.Seed, request.DisplayName, status);
        await _unitOfWork.SaveChangesAsync(ct);
        return entry.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<TeamStaffDto>>> ListStaffAsync(CompetitionTeamId competitionTeamId, CancellationToken ct = default)
    {
        var staff = await _staff.ListForCompetitionTeamAsync(competitionTeamId, ct);
        return Result.Success<IReadOnlyList<TeamStaffDto>>(staff.Select(s => s.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<TeamStaffDto>> AddStaffAsync(CompetitionTeamId competitionTeamId, AddStaffRequest request, CancellationToken ct = default)
    {
        var entry = await _entries.GetAsync(competitionTeamId, ct);
        if (entry is null)
        {
            return Error.NotFound("COMPETITION_TEAM_NOT_FOUND", "The competition entry does not exist.");
        }

        if (!TryParse<TeamStaffRole>(request.Role, out var role))
        {
            return Error.Validation("INVALID_ROLE", $"'{request.Role}' is not a valid staff role.");
        }

        var staff = TeamStaff.Create(entry.OrganisationId, competitionTeamId, request.FullName, role);
        _staff.Add(staff);
        await _unitOfWork.SaveChangesAsync(ct);
        return staff.ToDto();
    }

    private static Error CompetitionNotFound() => Error.NotFound("COMPETITION_NOT_FOUND", "The competition does not exist.");

    private static bool TryParse<TEnum>(string value, out TEnum result) where TEnum : struct, Enum
        => Enum.TryParse(value, ignoreCase: true, out result) && Enum.IsDefined(result);
}
