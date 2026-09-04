using Hoops.Api.Auth;
using Hoops.Modules.GameRecording.Contracts;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.GameRecording;

/// <summary>A single fixture: scheduling, officials, setup, and roster lock (§6 through RosterLocked).</summary>
[Route("api/v1/organisations/{orgId:guid}/games")]
public sealed class GamesController : ApiControllerBase
{
    private readonly IGameService _games;

    /// <summary>Creates the controller.</summary>
    public GamesController(IGameService games) => _games = games;

    /// <summary>Fetches a game.</summary>
    /// <response code="200">The game.</response>
    /// <response code="404">The game does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("{gameId:guid}")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameDto>> Get(Guid orgId, Guid gameId, CancellationToken ct)
        => Ok(await _games.GetAsync(GameId.FromGuid(gameId), ct));

    /// <summary>Reschedules a fixture (time/venue). Only before roster lock.</summary>
    /// <response code="200">The updated game.</response>
    /// <response code="404">The game does not exist.</response>
    /// <response code="409">The game can no longer be rescheduled.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPatch("{gameId:guid}")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameDto>> Reschedule(Guid orgId, Guid gameId, [FromBody] RescheduleGameRequest request, CancellationToken ct)
        => Ok(await _games.RescheduleAsync(GameId.FromGuid(gameId), request, ct));

    /// <summary>Deletes a fixture. Only while Scheduled.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="404">The game does not exist.</response>
    /// <response code="409">Only a scheduled game can be deleted.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpDelete("{gameId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(Guid orgId, Guid gameId, CancellationToken ct)
        => NoContent(await _games.DeleteAsync(GameId.FromGuid(gameId), ct));

    /// <summary>Postpones a scheduled game.</summary>
    /// <response code="200">The updated game.</response>
    /// <response code="404">The game does not exist.</response>
    /// <response code="409">Only a scheduled game can be postponed.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("{gameId:guid}/postpone")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameDto>> Postpone(Guid orgId, Guid gameId, CancellationToken ct)
        => Ok(await _games.PostponeAsync(GameId.FromGuid(gameId), ct));

    /// <summary>Cancels a scheduled or postponed game.</summary>
    /// <response code="200">The updated game.</response>
    /// <response code="404">The game does not exist.</response>
    /// <response code="409">The game cannot be cancelled from its current state.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("{gameId:guid}/cancel")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameDto>> Cancel(Guid orgId, Guid gameId, CancellationToken ct)
        => Ok(await _games.CancelAsync(GameId.FromGuid(gameId), ct));

    /// <summary>Lists a game's officials.</summary>
    /// <response code="200">The officials.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("{gameId:guid}/officials")]
    [ProducesResponseType(typeof(IReadOnlyList<GameOfficialDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<GameOfficialDto>>> ListOfficials(Guid orgId, Guid gameId, CancellationToken ct)
        => Ok(await _games.ListOfficialsAsync(GameId.FromGuid(gameId), ct));

    /// <summary>Assigns an official to a game.</summary>
    /// <response code="201">The assigned official.</response>
    /// <response code="400">Invalid role.</response>
    /// <response code="404">The game does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("{gameId:guid}/officials")]
    [ProducesResponseType(typeof(GameOfficialDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameOfficialDto>> AssignOfficial(Guid orgId, Guid gameId, [FromBody] AssignOfficialRequest request, CancellationToken ct)
        => Created(await _games.AssignOfficialAsync(OrganisationId.FromGuid(orgId), GameId.FromGuid(gameId), request, ct));

    /// <summary>The setup screen: fixture, rule set, and both teams' current rosters.</summary>
    /// <response code="200">The setup.</response>
    /// <response code="404">The game does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("{gameId:guid}/setup")]
    [ProducesResponseType(typeof(GameSetupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameSetupDto>> Setup(Guid orgId, Guid gameId, CancellationToken ct)
        => Ok(await _games.GetSetupAsync(GameId.FromGuid(gameId), ct));

    /// <summary>Locks the roster: validates size and starters, freezes a roster and rule-set snapshot.</summary>
    /// <response code="200">The locked game.</response>
    /// <response code="400">Roster too small, or wrong starter count.</response>
    /// <response code="404">The game does not exist.</response>
    /// <response code="409">The game's roster cannot be locked from its current state.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("{gameId:guid}/lock-roster")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameDto>> LockRoster(Guid orgId, Guid gameId, [FromBody] LockRosterRequest request, CancellationToken ct)
        => Ok(await _games.LockRosterAsync(GameId.FromGuid(gameId), request, ct));

    /// <summary>The frozen game-roster snapshot.</summary>
    /// <response code="200">The snapshot roster.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("{gameId:guid}/roster")]
    [ProducesResponseType(typeof(IReadOnlyList<GameRosterEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<GameRosterEntryDto>>> GameRoster(Guid orgId, Guid gameId, CancellationToken ct)
        => Ok(await _games.GetGameRosterAsync(GameId.FromGuid(gameId), ct));
}
