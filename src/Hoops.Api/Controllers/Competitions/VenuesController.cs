using Hoops.Api.Auth;
using Hoops.Modules.Competitions.Contracts;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.Competitions;

/// <summary>Venues within an organisation.</summary>
[Route("api/v1/organisations/{orgId:guid}/venues")]
public sealed class VenuesController : ApiControllerBase
{
    private readonly IVenueService _venues;

    /// <summary>Creates the controller.</summary>
    public VenuesController(IVenueService venues) => _venues = venues;

    /// <summary>Lists the organisation's venues.</summary>
    /// <response code="200">The venues.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<VenueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<VenueDto>>> List(Guid orgId, CancellationToken ct)
        => Ok(await _venues.ListAsync(OrganisationId.FromGuid(orgId), ct));

    /// <summary>Creates a venue.</summary>
    /// <response code="201">The created venue.</response>
    /// <response code="400">The payload was invalid.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost]
    [ProducesResponseType(typeof(VenueDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VenueDto>> Create(Guid orgId, [FromBody] CreateVenueRequest request, CancellationToken ct)
        => Created(await _venues.CreateAsync(OrganisationId.FromGuid(orgId), request, ct));

    /// <summary>Fetches a venue.</summary>
    /// <response code="200">The venue.</response>
    /// <response code="404">The venue does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("{venueId:guid}")]
    [ProducesResponseType(typeof(VenueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VenueDto>> Get(Guid orgId, Guid venueId, CancellationToken ct)
        => Ok(await _venues.GetAsync(VenueId.FromGuid(venueId), ct));

    /// <summary>Updates a venue.</summary>
    /// <response code="200">The updated venue.</response>
    /// <response code="400">The payload was invalid.</response>
    /// <response code="404">The venue does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPatch("{venueId:guid}")]
    [ProducesResponseType(typeof(VenueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VenueDto>> Update(Guid orgId, Guid venueId, [FromBody] UpdateVenueRequest request, CancellationToken ct)
        => Ok(await _venues.UpdateAsync(VenueId.FromGuid(venueId), request, ct));
}
