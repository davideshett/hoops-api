using Hoops.Api.Auth;
using Hoops.Api.Http;
using Hoops.Modules.GameRecording.Contracts;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.GameRecording;

/// <summary>
/// The event write path (§11). Submission is idempotent on the client-generated <c>eventId</c> and
/// detects stale sequences, so a request that times out after the server committed is safe to retry.
/// </summary>
[Route("api/v1/organisations/{orgId:guid}/games/{gameId:guid}")]
public sealed class GameEventsController : ApiControllerBase
{
    /// <summary>Header carrying the reason when a tier-2 validation rule is overridden.</summary>
    public const string OverrideReasonHeader = "X-Override-Reason";

    private readonly IEventRecordingService _recording;
    private readonly ICurrentUser _currentUser;

    /// <summary>Creates the controller.</summary>
    public GameEventsController(IEventRecordingService recording, ICurrentUser currentUser)
    {
        _recording = recording;
        _currentUser = currentUser;
    }

    private UserId CurrentUserId => UserId.FromGuid(_currentUser.UserId!.Value);

    /// <summary>Starts the game (RosterLocked → InProgress).</summary>
    /// <response code="200">The live state after GAME_START.</response>
    /// <response code="409">The game cannot be started from its current status.</response>
    [Authorize(Policy = AuthPolicies.OrgStatistician)]
    [HttpPost("start")]
    [ProducesResponseType(typeof(LiveGameStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LiveGameStateDto>> Start(Guid orgId, Guid gameId, CancellationToken ct)
        => Ok(await _recording.StartGameAsync(GameId.FromGuid(gameId), CurrentUserId, ct));

    /// <summary>Ends the game (InProgress → PendingReview).</summary>
    /// <response code="200">The live state after GAME_END.</response>
    /// <response code="400">The game cannot end tied unless the rule set allows it.</response>
    /// <response code="409">The game is not in progress.</response>
    [Authorize(Policy = AuthPolicies.OrgStatistician)]
    [HttpPost("end")]
    [ProducesResponseType(typeof(LiveGameStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LiveGameStateDto>> End(Guid orgId, Guid gameId, CancellationToken ct)
        => Ok(await _recording.EndGameAsync(GameId.FromGuid(gameId), CurrentUserId, ct));

    /// <summary>
    /// Submits ONE event. Replaying an already-recorded <c>eventId</c> returns <c>200</c> with the
    /// original result; a stale <c>lastKnownSequence</c> returns <c>409</c> with the missing events.
    /// </summary>
    /// <response code="201">Accepted; the body carries the sequence and the full live state.</response>
    /// <response code="200">Idempotent replay of an event already recorded.</response>
    /// <response code="400">A validation rule rejected the event (§10).</response>
    /// <response code="409">The client's last known sequence is stale.</response>
    [Authorize(Policy = AuthPolicies.OrgStatistician)]
    [HttpPost("events")]
    [ProducesResponseType(typeof(EventAcceptedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(EventAcceptedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventAcceptedDto>> Submit(
        Guid orgId, Guid gameId, [FromBody] SubmitEventRequest request, [FromQuery] bool @override, CancellationToken ct)
    {
        var id = GameId.FromGuid(gameId);
        var alreadyRecorded = await _recording.ListEventsAsync(id, null, ct);
        var isReplay = alreadyRecorded.IsSuccess && alreadyRecorded.Value.Any(e => e.EventId == request.EventId);

        var result = await _recording.SubmitAsync(id, CurrentUserId, request, @override, OverrideReason(), ct);
        if (result.IsFailure)
        {
            return ErrorResults.ToProblem(result.Error, HttpContext);
        }

        // 200 for an idempotent replay, 201 for a newly accepted event.
        return isReplay ? base.Ok(result.Value) : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Submits an ordered batch (free-throw sequences, substitution groups).</summary>
    /// <response code="201">Accepted; the body carries the last sequence and the full live state.</response>
    /// <response code="400">A validation rule rejected one of the events.</response>
    /// <response code="409">The client's last known sequence is stale.</response>
    [Authorize(Policy = AuthPolicies.OrgStatistician)]
    [HttpPost("events/batch")]
    [ProducesResponseType(typeof(EventAcceptedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventAcceptedDto>> SubmitBatch(
        Guid orgId, Guid gameId, [FromBody] SubmitEventBatchRequest request, [FromQuery] bool @override, CancellationToken ct)
        => Created(await _recording.SubmitBatchAsync(GameId.FromGuid(gameId), CurrentUserId, request, @override, OverrideReason(), ct));

    /// <summary>Lists the game's events; pass <c>sinceSequence</c> to resync after a dropped connection.</summary>
    /// <response code="200">The events.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("events")]
    [ProducesResponseType(typeof(IReadOnlyList<GameEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<GameEventDto>>> ListEvents(
        Guid orgId, Guid gameId, [FromQuery] long? sinceSequence, CancellationToken ct)
        => Ok(await _recording.ListEventsAsync(GameId.FromGuid(gameId), sinceSequence, ct));

    /// <summary>Voids one event. The row is retained; only its contribution is removed.</summary>
    /// <response code="201">The VOID event was appended.</response>
    /// <response code="404">The event does not exist in this game.</response>
    /// <response code="409">That event is already voided.</response>
    [Authorize(Policy = AuthPolicies.OrgStatistician)]
    [HttpPost("events/{eventId:guid}/void")]
    [ProducesResponseType(typeof(EventAcceptedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventAcceptedDto>> VoidEvent(
        Guid orgId, Guid gameId, Guid eventId, [FromBody] VoidEventRequest request, CancellationToken ct)
        => Created(await _recording.VoidAsync(GameId.FromGuid(gameId), CurrentUserId, eventId, request, ct));

    /// <summary>Voids the highest non-voided event, skipping ones already voided.</summary>
    /// <response code="201">The VOID event was appended.</response>
    /// <response code="409">There is nothing left to undo.</response>
    [Authorize(Policy = AuthPolicies.OrgStatistician)]
    [HttpPost("events/undo-last")]
    [ProducesResponseType(typeof(EventAcceptedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventAcceptedDto>> UndoLast(Guid orgId, Guid gameId, CancellationToken ct)
        => Created(await _recording.UndoLastAsync(GameId.FromGuid(gameId), CurrentUserId, ct));

    /// <summary>The live state — the recording app's home screen, derived from the log.</summary>
    /// <response code="200">The live state.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("state")]
    [ProducesResponseType(typeof(LiveGameStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LiveGameStateDto>> State(Guid orgId, Guid gameId, CancellationToken ct)
        => Ok(await _recording.GetStateAsync(GameId.FromGuid(gameId), ct));

    /// <summary>The box score, derived by replaying the log.</summary>
    /// <response code="200">The box score.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("box-score")]
    [ProducesResponseType(typeof(BoxScoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BoxScoreDto>> BoxScore(Guid orgId, Guid gameId, CancellationToken ct)
        => Ok(await _recording.GetBoxScoreAsync(GameId.FromGuid(gameId), ct));

    private string? OverrideReason()
        => Request.Headers.TryGetValue(OverrideReasonHeader, out var value) ? value.ToString() : null;
}
