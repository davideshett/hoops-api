using Hoops.Api.Auth;
using Hoops.Modules.Competitions.Contracts;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.Competitions;

/// <summary>Seasons within an organisation.</summary>
[Route("api/v1/organisations/{orgId:guid}/seasons")]
public sealed class SeasonsController : ApiControllerBase
{
    private readonly ISeasonService _seasons;

    /// <summary>Creates the controller.</summary>
    public SeasonsController(ISeasonService seasons) => _seasons = seasons;

    /// <summary>Lists the organisation's seasons.</summary>
    /// <response code="200">The seasons.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SeasonDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SeasonDto>>> List(Guid orgId, CancellationToken ct)
        => Ok(await _seasons.ListAsync(OrganisationId.FromGuid(orgId), ct));

    /// <summary>Creates a season.</summary>
    /// <response code="201">The created season.</response>
    /// <response code="400">The payload was invalid.</response>
    /// <response code="409">A season with this name already exists.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost]
    [ProducesResponseType(typeof(SeasonDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SeasonDto>> Create(Guid orgId, [FromBody] CreateSeasonRequest request, CancellationToken ct)
        => Created(await _seasons.CreateAsync(OrganisationId.FromGuid(orgId), request, ct));

    /// <summary>Fetches a season.</summary>
    /// <response code="200">The season.</response>
    /// <response code="404">The season does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("{seasonId:guid}")]
    [ProducesResponseType(typeof(SeasonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonDto>> Get(Guid orgId, Guid seasonId, CancellationToken ct)
        => Ok(await _seasons.GetAsync(SeasonId.FromGuid(seasonId), ct));

    /// <summary>Updates a season.</summary>
    /// <response code="200">The updated season.</response>
    /// <response code="400">The payload was invalid.</response>
    /// <response code="404">The season does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPatch("{seasonId:guid}")]
    [ProducesResponseType(typeof(SeasonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonDto>> Update(Guid orgId, Guid seasonId, [FromBody] UpdateSeasonRequest request, CancellationToken ct)
        => Ok(await _seasons.UpdateAsync(SeasonId.FromGuid(seasonId), request, ct));
}
