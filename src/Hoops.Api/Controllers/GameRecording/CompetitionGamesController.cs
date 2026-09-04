using Hoops.Api.Auth;
using Hoops.Modules.GameRecording.Contracts;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.GameRecording;

/// <summary>Fixtures within a competition: listing, single creation, and schedule generation.</summary>
[Route("api/v1/organisations/{orgId:guid}/competitions/{competitionId:guid}/games")]
public sealed class CompetitionGamesController : ApiControllerBase
{
    private readonly IGameService _games;

    /// <summary>Creates the controller.</summary>
    public CompetitionGamesController(IGameService games) => _games = games;

    /// <summary>Lists a competition's games, optionally filtered by stage and status.</summary>
    /// <response code="200">The games.</response>
    /// <response code="400">Invalid status filter.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<GameDto>>> List(
        Guid orgId, Guid competitionId, [FromQuery] Guid? stageId, [FromQuery] string? status, CancellationToken ct)
        => Ok(await _games.ListAsync(
            CompetitionId.FromGuid(competitionId),
            stageId.HasValue ? StageId.FromGuid(stageId.Value) : null, status, ct));

    /// <summary>Creates a single fixture.</summary>
    /// <response code="201">The created fixture.</response>
    /// <response code="400">Invalid payload, or a team not entered / scheduled against itself.</response>
    /// <response code="404">The competition does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameDto>> Create(Guid orgId, Guid competitionId, [FromBody] CreateGameRequest request, CancellationToken ct)
        => Created(await _games.CreateAsync(OrganisationId.FromGuid(orgId), CompetitionId.FromGuid(competitionId), request, ct));

    /// <summary>Generates a round-robin schedule (single or double) from the entered teams.</summary>
    /// <response code="200">The generated fixtures.</response>
    /// <response code="400">Fewer than two teams are entered.</response>
    /// <response code="404">The competition does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("generate/round-robin")]
    [ProducesResponseType(typeof(IReadOnlyList<GameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<GameDto>>> GenerateRoundRobin(
        Guid orgId, Guid competitionId, [FromBody] GenerateRoundRobinRequest request, CancellationToken ct)
        => Ok(await _games.GenerateRoundRobinAsync(OrganisationId.FromGuid(orgId), CompetitionId.FromGuid(competitionId), request, ct));

    /// <summary>Generates a seeded knockout bracket's first round from the entered teams.</summary>
    /// <response code="200">The generated fixtures.</response>
    /// <response code="400">Fewer than two teams are entered.</response>
    /// <response code="404">The competition does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("generate/knockout")]
    [ProducesResponseType(typeof(IReadOnlyList<GameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<GameDto>>> GenerateKnockout(
        Guid orgId, Guid competitionId, [FromBody] GenerateKnockoutRequest request, CancellationToken ct)
        => Ok(await _games.GenerateKnockoutAsync(OrganisationId.FromGuid(orgId), CompetitionId.FromGuid(competitionId), request, ct));
}
