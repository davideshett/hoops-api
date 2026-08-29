using Hoops.Api.Auth;
using Hoops.Modules.Competitions.Contracts;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.Competitions;

/// <summary>Competitions, their stages, and their team entries.</summary>
[Route("api/v1/organisations/{orgId:guid}/competitions")]
public sealed class CompetitionsController : ApiControllerBase
{
    private readonly ICompetitionService _competitions;

    /// <summary>Creates the controller.</summary>
    public CompetitionsController(ICompetitionService competitions) => _competitions = competitions;

    /// <summary>Lists competitions, optionally filtered by season and/or status.</summary>
    /// <response code="200">The competitions.</response>
    /// <response code="400">The status filter was invalid.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CompetitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CompetitionDto>>> List(
        Guid orgId, [FromQuery] Guid? seasonId, [FromQuery] string? status, CancellationToken ct)
        => Ok(await _competitions.ListAsync(
            OrganisationId.FromGuid(orgId),
            seasonId.HasValue ? SeasonId.FromGuid(seasonId.Value) : null,
            status, ct));

    /// <summary>Creates a competition (with an optional custom rule set; defaults to FIBA).</summary>
    /// <response code="201">The created competition.</response>
    /// <response code="400">The payload was invalid or referenced a missing season.</response>
    /// <response code="409">A competition with this slug already exists in the season.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost]
    [ProducesResponseType(typeof(CompetitionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompetitionDto>> Create(Guid orgId, [FromBody] CreateCompetitionRequest request, CancellationToken ct)
        => Created(await _competitions.CreateAsync(OrganisationId.FromGuid(orgId), request, ct));

    /// <summary>Fetches a competition.</summary>
    /// <response code="200">The competition.</response>
    /// <response code="404">The competition does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("{competitionId:guid}")]
    [ProducesResponseType(typeof(CompetitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompetitionDto>> Get(Guid orgId, Guid competitionId, CancellationToken ct)
        => Ok(await _competitions.GetAsync(CompetitionId.FromGuid(competitionId), ct));

    /// <summary>Updates a competition's profile (including its rule set).</summary>
    /// <response code="200">The updated competition.</response>
    /// <response code="400">The payload was invalid.</response>
    /// <response code="404">The competition does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPatch("{competitionId:guid}")]
    [ProducesResponseType(typeof(CompetitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompetitionDto>> Update(Guid orgId, Guid competitionId, [FromBody] UpdateCompetitionRequest request, CancellationToken ct)
        => Ok(await _competitions.UpdateAsync(CompetitionId.FromGuid(competitionId), request, ct));

    /// <summary>Moves a competition to a new lifecycle status.</summary>
    /// <response code="200">The updated competition.</response>
    /// <response code="400">The status was invalid.</response>
    /// <response code="404">The competition does not exist.</response>
    /// <response code="409">The status transition is not allowed from the current status.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("{competitionId:guid}/status")]
    [ProducesResponseType(typeof(CompetitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompetitionDto>> ChangeStatus(Guid orgId, Guid competitionId, [FromBody] ChangeCompetitionStatusRequest request, CancellationToken ct)
        => Ok(await _competitions.ChangeStatusAsync(CompetitionId.FromGuid(competitionId), request, ct));

    /// <summary>Lists a competition's stages.</summary>
    /// <response code="200">The stages.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("{competitionId:guid}/stages")]
    [ProducesResponseType(typeof(IReadOnlyList<StageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<StageDto>>> ListStages(Guid orgId, Guid competitionId, CancellationToken ct)
        => Ok(await _competitions.ListStagesAsync(CompetitionId.FromGuid(competitionId), ct));

    /// <summary>Adds a stage to a competition.</summary>
    /// <response code="201">The created stage.</response>
    /// <response code="400">The payload was invalid.</response>
    /// <response code="404">The competition does not exist.</response>
    /// <response code="409">A stage with this sequence already exists.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("{competitionId:guid}/stages")]
    [ProducesResponseType(typeof(StageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StageDto>> CreateStage(Guid orgId, Guid competitionId, [FromBody] CreateStageRequest request, CancellationToken ct)
        => Created(await _competitions.CreateStageAsync(CompetitionId.FromGuid(competitionId), request, ct));

    /// <summary>Lists a competition's team entries.</summary>
    /// <response code="200">The entries.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("{competitionId:guid}/teams")]
    [ProducesResponseType(typeof(IReadOnlyList<CompetitionTeamDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CompetitionTeamDto>>> ListTeams(Guid orgId, Guid competitionId, CancellationToken ct)
        => Ok(await _competitions.ListTeamsAsync(CompetitionId.FromGuid(competitionId), ct));

    /// <summary>Enters an existing canonical team into a competition.</summary>
    /// <response code="201">The created entry.</response>
    /// <response code="400">The payload was invalid or referenced a missing team.</response>
    /// <response code="404">The competition does not exist.</response>
    /// <response code="409">That team is already entered in this competition.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("{competitionId:guid}/teams")]
    [ProducesResponseType(typeof(CompetitionTeamDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompetitionTeamDto>> EnterTeam(Guid orgId, Guid competitionId, [FromBody] EnterTeamRequest request, CancellationToken ct)
        => Created(await _competitions.EnterTeamAsync(CompetitionId.FromGuid(competitionId), request, ct));
}
