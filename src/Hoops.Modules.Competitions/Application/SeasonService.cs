using Hoops.Modules.Competitions.Application.Abstractions;
using Hoops.Modules.Competitions.Contracts;
using Hoops.Modules.Competitions.Domain;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Competitions.Application;

/// <summary>Season use cases.</summary>
public sealed class SeasonService : ISeasonService
{
    private readonly ISeasonRepository _seasons;
    private readonly ICompetitionsUnitOfWork _unitOfWork;

    /// <summary>Creates the service.</summary>
    public SeasonService(ISeasonRepository seasons, ICompetitionsUnitOfWork unitOfWork)
    {
        _seasons = seasons;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<SeasonDto>>> ListAsync(OrganisationId organisationId, CancellationToken ct = default)
    {
        var seasons = await _seasons.ListAsync(organisationId, ct);
        return Result.Success<IReadOnlyList<SeasonDto>>(seasons.Select(s => s.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<SeasonDto>> CreateAsync(OrganisationId organisationId, CreateSeasonRequest request, CancellationToken ct = default)
    {
        if (await _seasons.ExistsByNameAsync(organisationId, request.Name.Trim(), ct))
        {
            return Error.Conflict("SEASON_NAME_TAKEN", "A season with this name already exists.");
        }

        Season season;
        try
        {
            season = Season.Create(organisationId, request.Name, request.StartsOn, request.EndsOn);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("INVALID_SEASON", ex.Message);
        }

        _seasons.Add(season);
        await _unitOfWork.SaveChangesAsync(ct);
        return season.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<SeasonDto>> GetAsync(SeasonId id, CancellationToken ct = default)
    {
        var season = await _seasons.GetAsync(id, ct);
        return season is null ? NotFound() : season.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<SeasonDto>> UpdateAsync(SeasonId id, UpdateSeasonRequest request, CancellationToken ct = default)
    {
        var season = await _seasons.GetAsync(id, ct);
        if (season is null)
        {
            return NotFound();
        }

        try
        {
            season.Update(request.Name, request.StartsOn, request.EndsOn, request.IsActive);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("INVALID_SEASON", ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return season.ToDto();
    }

    private static Error NotFound() => Error.NotFound("SEASON_NOT_FOUND", "The season does not exist.");
}
