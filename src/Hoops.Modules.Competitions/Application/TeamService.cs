using Hoops.Modules.Competitions.Application.Abstractions;
using Hoops.Modules.Competitions.Contracts;
using Hoops.Modules.Competitions.Domain;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Competitions.Application;

/// <summary>Canonical team use cases.</summary>
public sealed class TeamService : ITeamService
{
    private readonly ITeamRepository _teams;
    private readonly ICompetitionsUnitOfWork _unitOfWork;

    /// <summary>Creates the service.</summary>
    public TeamService(ITeamRepository teams, ICompetitionsUnitOfWork unitOfWork)
    {
        _teams = teams;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<TeamDto>>> ListAsync(OrganisationId organisationId, string? search, CancellationToken ct = default)
    {
        var teams = await _teams.ListAsync(organisationId, search, ct);
        return Result.Success<IReadOnlyList<TeamDto>>(teams.Select(t => t.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<TeamDto>> CreateAsync(OrganisationId organisationId, CreateTeamRequest request, CancellationToken ct = default)
    {
        Team team;
        try
        {
            team = Team.Create(
                organisationId, request.Name, request.ShortName, request.Abbreviation,
                request.PrimaryColour, request.SecondaryColour, request.HomeVenueId);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("INVALID_TEAM", ex.Message);
        }

        _teams.Add(team);
        await _unitOfWork.SaveChangesAsync(ct);
        return team.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<TeamDto>> GetAsync(TeamId id, CancellationToken ct = default)
    {
        var team = await _teams.GetAsync(id, ct);
        return team is null ? NotFound() : team.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<TeamDto>> UpdateAsync(TeamId id, UpdateTeamRequest request, CancellationToken ct = default)
    {
        var team = await _teams.GetAsync(id, ct);
        if (team is null)
        {
            return NotFound();
        }

        try
        {
            team.Update(request.Name, request.ShortName, request.Abbreviation,
                request.PrimaryColour, request.SecondaryColour, request.HomeVenueId);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("INVALID_TEAM", ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return team.ToDto();
    }

    private static Error NotFound() => Error.NotFound("TEAM_NOT_FOUND", "The team does not exist.");
}
