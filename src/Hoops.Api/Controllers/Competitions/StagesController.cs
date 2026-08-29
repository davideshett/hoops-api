using Hoops.Api.Auth;
using Hoops.Modules.Competitions.Contracts;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.Competitions;

/// <summary>Groups (pools) within a stage.</summary>
[Route("api/v1/organisations/{orgId:guid}/stages/{stageId:guid}/groups")]
public sealed class StagesController : ApiControllerBase
{
    private readonly ICompetitionService _competitions;

    /// <summary>Creates the controller.</summary>
    public StagesController(ICompetitionService competitions) => _competitions = competitions;

    /// <summary>Lists a stage's groups.</summary>
    /// <response code="200">The groups.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<GroupDto>>> ListGroups(Guid orgId, Guid stageId, CancellationToken ct)
        => Ok(await _competitions.ListGroupsAsync(StageId.FromGuid(stageId), ct));

    /// <summary>Adds a group to a stage.</summary>
    /// <response code="201">The created group.</response>
    /// <response code="400">The payload was invalid.</response>
    /// <response code="404">The stage does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost]
    [ProducesResponseType(typeof(GroupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupDto>> CreateGroup(Guid orgId, Guid stageId, [FromBody] CreateGroupRequest request, CancellationToken ct)
        => Created(await _competitions.CreateGroupAsync(StageId.FromGuid(stageId), request, ct));
}
