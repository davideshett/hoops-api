using Hoops.Api.Auth;
using Hoops.Modules.Registry.Contracts;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.Registry;

/// <summary>
/// Rosters — a competition team's squad, built from registry player ids only (§5A.1). A body with a
/// name instead of a playerId is rejected.
/// </summary>
[Route("api/v1/organisations/{orgId:guid}")]
public sealed class RostersController : RegistryControllerBase
{
    private readonly IRosterService _rosters;

    /// <summary>Creates the controller.</summary>
    public RostersController(IRosterService rosters, ICurrentUser currentUser) : base(currentUser)
        => _rosters = rosters;

    /// <summary>Lists a squad's roster.</summary>
    /// <response code="200">The roster entries.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("competition-teams/{competitionTeamId:guid}/roster")]
    [ProducesResponseType(typeof(IReadOnlyList<RosterEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RosterEntryDto>>> List(Guid orgId, Guid competitionTeamId, CancellationToken ct)
        => Ok(await _rosters.ListAsync(CompetitionTeamId.FromGuid(competitionTeamId), ct));

    /// <summary>Adds a registry player to a squad by playerId, enforcing jersey uniqueness.</summary>
    /// <response code="201">The created roster entry.</response>
    /// <response code="400">Missing/invalid playerId (e.g. a name was sent instead), or unknown player.</response>
    /// <response code="404">The competition team does not exist.</response>
    /// <response code="409">The jersey is taken, or the player is already on the squad.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("competition-teams/{competitionTeamId:guid}/roster")]
    [ProducesResponseType(typeof(RosterEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RosterEntryDto>> Register(
        Guid orgId, Guid competitionTeamId, [FromBody] RegisterRosterEntryRequest request, CancellationToken ct)
        => Created(await _rosters.RegisterAsync(CallerFor(orgId), CompetitionTeamId.FromGuid(competitionTeamId), request, ct));

    /// <summary>Updates a roster entry (jersey, position, captaincy, status).</summary>
    /// <response code="200">The updated entry.</response>
    /// <response code="400">Invalid status.</response>
    /// <response code="404">The entry does not exist.</response>
    /// <response code="409">The jersey is taken.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPatch("roster-entries/{rosterEntryId:guid}")]
    [ProducesResponseType(typeof(RosterEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RosterEntryDto>> Update(Guid orgId, Guid rosterEntryId, [FromBody] UpdateRosterEntryRequest request, CancellationToken ct)
        => Ok(await _rosters.UpdateAsync(RosterEntryId.FromGuid(rosterEntryId), request, ct));

    /// <summary>Removes a player from a squad (frees the jersey number).</summary>
    /// <response code="204">The entry was removed.</response>
    /// <response code="404">The entry does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpDelete("roster-entries/{rosterEntryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Remove(Guid orgId, Guid rosterEntryId, CancellationToken ct)
        => NoContent(await _rosters.RemoveAsync(RosterEntryId.FromGuid(rosterEntryId), ct));
}
