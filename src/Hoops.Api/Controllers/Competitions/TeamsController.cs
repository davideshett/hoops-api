using Hoops.Api.Auth;
using Hoops.Modules.Competitions.Contracts;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.Competitions;

/// <summary>Canonical teams (clubs) — reusable across an organisation's competitions.</summary>
[Route("api/v1/organisations/{orgId:guid}/teams")]
public sealed class TeamsController : ApiControllerBase
{
    private readonly ITeamService _teams;

    /// <summary>Creates the controller.</summary>
    public TeamsController(ITeamService teams) => _teams = teams;

    /// <summary>Lists the organisation's teams, optionally filtered by a name search.</summary>
    /// <response code="200">The teams.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TeamDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> List(Guid orgId, [FromQuery] string? search, CancellationToken ct)
        => Ok(await _teams.ListAsync(OrganisationId.FromGuid(orgId), search, ct));

    /// <summary>Creates a canonical team.</summary>
    /// <response code="201">The created team.</response>
    /// <response code="400">The payload was invalid.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TeamDto>> Create(Guid orgId, [FromBody] CreateTeamRequest request, CancellationToken ct)
        => Created(await _teams.CreateAsync(OrganisationId.FromGuid(orgId), request, ct));

    /// <summary>Fetches a team.</summary>
    /// <response code="200">The team.</response>
    /// <response code="404">The team does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("{teamId:guid}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> Get(Guid orgId, Guid teamId, CancellationToken ct)
        => Ok(await _teams.GetAsync(TeamId.FromGuid(teamId), ct));

    /// <summary>Updates a team.</summary>
    /// <response code="200">The updated team.</response>
    /// <response code="400">The payload was invalid.</response>
    /// <response code="404">The team does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPatch("{teamId:guid}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> Update(Guid orgId, Guid teamId, [FromBody] UpdateTeamRequest request, CancellationToken ct)
        => Ok(await _teams.UpdateAsync(TeamId.FromGuid(teamId), request, ct));
}
