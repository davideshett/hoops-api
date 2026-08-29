using Hoops.Api.Auth;
using Hoops.Modules.Competitions.Contracts;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.Competitions;

/// <summary>Competition entries — group/seed/status changes and coaching staff.</summary>
[Route("api/v1/organisations/{orgId:guid}/competition-teams")]
public sealed class CompetitionTeamsController : ApiControllerBase
{
    private readonly ICompetitionService _competitions;

    /// <summary>Creates the controller.</summary>
    public CompetitionTeamsController(ICompetitionService competitions) => _competitions = competitions;

    /// <summary>Updates a competition entry (group, seed, display name, status).</summary>
    /// <response code="200">The updated entry.</response>
    /// <response code="400">The payload was invalid.</response>
    /// <response code="404">The entry does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPatch("{competitionTeamId:guid}")]
    [ProducesResponseType(typeof(CompetitionTeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompetitionTeamDto>> UpdateEntry(Guid orgId, Guid competitionTeamId, [FromBody] UpdateCompetitionTeamRequest request, CancellationToken ct)
        => Ok(await _competitions.UpdateEntryAsync(CompetitionTeamId.FromGuid(competitionTeamId), request, ct));

    /// <summary>Lists staff on a competition entry.</summary>
    /// <response code="200">The staff.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("{competitionTeamId:guid}/staff")]
    [ProducesResponseType(typeof(IReadOnlyList<TeamStaffDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TeamStaffDto>>> ListStaff(Guid orgId, Guid competitionTeamId, CancellationToken ct)
        => Ok(await _competitions.ListStaffAsync(CompetitionTeamId.FromGuid(competitionTeamId), ct));

    /// <summary>Adds a staff member to a competition entry.</summary>
    /// <response code="201">The created staff member.</response>
    /// <response code="400">The payload was invalid.</response>
    /// <response code="404">The entry does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("{competitionTeamId:guid}/staff")]
    [ProducesResponseType(typeof(TeamStaffDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamStaffDto>> AddStaff(Guid orgId, Guid competitionTeamId, [FromBody] AddStaffRequest request, CancellationToken ct)
        => Created(await _competitions.AddStaffAsync(CompetitionTeamId.FromGuid(competitionTeamId), request, ct));
}
