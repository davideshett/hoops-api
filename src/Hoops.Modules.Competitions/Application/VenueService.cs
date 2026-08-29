using Hoops.Modules.Competitions.Application.Abstractions;
using Hoops.Modules.Competitions.Contracts;
using Hoops.Modules.Competitions.Domain;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Competitions.Application;

/// <summary>Venue use cases.</summary>
public sealed class VenueService : IVenueService
{
    private readonly IVenueRepository _venues;
    private readonly ICompetitionsUnitOfWork _unitOfWork;

    /// <summary>Creates the service.</summary>
    public VenueService(IVenueRepository venues, ICompetitionsUnitOfWork unitOfWork)
    {
        _venues = venues;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<VenueDto>>> ListAsync(OrganisationId organisationId, CancellationToken ct = default)
    {
        var venues = await _venues.ListAsync(organisationId, ct);
        return Result.Success<IReadOnlyList<VenueDto>>(venues.Select(v => v.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<VenueDto>> CreateAsync(OrganisationId organisationId, CreateVenueRequest request, CancellationToken ct = default)
    {
        Venue venue;
        try
        {
            venue = Venue.Create(organisationId, request.Name, request.Address, request.City, request.CourtCount ?? 1, request.Timezone);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("INVALID_VENUE", ex.Message);
        }

        _venues.Add(venue);
        await _unitOfWork.SaveChangesAsync(ct);
        return venue.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<VenueDto>> GetAsync(VenueId id, CancellationToken ct = default)
    {
        var venue = await _venues.GetAsync(id, ct);
        return venue is null ? NotFound() : venue.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<VenueDto>> UpdateAsync(VenueId id, UpdateVenueRequest request, CancellationToken ct = default)
    {
        var venue = await _venues.GetAsync(id, ct);
        if (venue is null)
        {
            return NotFound();
        }

        try
        {
            venue.Update(request.Name, request.Address, request.City, request.CourtCount, request.Timezone);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("INVALID_VENUE", ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return venue.ToDto();
    }

    private static Error NotFound() => Error.NotFound("VENUE_NOT_FOUND", "The venue does not exist.");
}
