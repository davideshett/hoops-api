using Hoops.Modules.GameRecording.Application.Abstractions;
using Hoops.Modules.GameRecording.Contracts;
using Hoops.Modules.GameRecording.Domain;
using Hoops.Modules.GameRecording.Domain.Projection;
using Hoops.Modules.GameRecording.Domain.Validation;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Hoops.Modules.GameRecording.Application;

/// <summary>
/// The event write path. Submission is idempotent on the client-generated event id and detects stale
/// sequences, so a request that times out after the server committed is safe to retry (ADR-007).
/// Live state is always derived by replaying the log — never accumulated separately.
/// </summary>
public sealed class EventRecordingService : IEventRecordingService
{
    private readonly IGameRepository _games;
    private readonly IGameEventRepository _events;
    private readonly IGameRosterRepository _rosters;
    private readonly IGameRecordingUnitOfWork _unitOfWork;
    private readonly IGameProjector _projector;
    private readonly EventValidationPipeline _pipeline = new();
    private readonly IClock _clock;
    private readonly ILogger<EventRecordingService> _logger;

    /// <summary>Creates the service.</summary>
    public EventRecordingService(
        IGameRepository games, IGameEventRepository events, IGameRosterRepository rosters,
        IGameRecordingUnitOfWork unitOfWork, IGameProjector projector, IClock clock,
        ILogger<EventRecordingService> logger)
    {
        _games = games;
        _events = events;
        _rosters = rosters;
        _unitOfWork = unitOfWork;
        _projector = projector;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<LiveGameStateDto>> StartGameAsync(GameId gameId, UserId userId, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(gameId, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        if (!game.Start(_clock.UtcNow))
        {
            return Error.Conflict("GAME_NOT_STARTABLE", "Only a game with a locked roster can be started.");
        }

        var request = new SubmitEventRequest(
            Guid.CreateVersion7(), null, EventTypes.GameStart, null, 1,
            (game.RuleSetSnapshot?.PeriodDurationSeconds ?? 600) * 1000,
            null, null, null, null, null, null, null, null, null);

        var accepted = await SubmitInternalAsync(game, userId, [request], allowOverride: false, overrideReason: null, ct);
        return accepted.IsFailure ? accepted.Error : accepted.Value.State;
    }

    /// <inheritdoc />
    public async Task<Result<LiveGameStateDto>> EndGameAsync(GameId gameId, UserId userId, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(gameId, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        if (game.Status != GameStatus.InProgress)
        {
            return Error.Conflict("GAME_NOT_IN_PROGRESS", "Only a game in progress can be ended.");
        }

        var (context, events) = await LoadAsync(game, ct);
        var state = _projector.Project(context, events).LiveState;

        var request = new SubmitEventRequest(
            Guid.CreateVersion7(), null, EventTypes.GameEnd, null,
            state.CurrentPeriod == 0 ? 1 : state.CurrentPeriod, state.GameClockMs,
            null, null, null, null, null, null, null, null, null);

        var accepted = await SubmitInternalAsync(game, userId, [request], allowOverride: false, overrideReason: null, ct);
        if (accepted.IsFailure)
        {
            return accepted.Error;
        }

        game.EndPlay();
        await _unitOfWork.SaveChangesAsync(ct);
        return accepted.Value.State;
    }

    /// <inheritdoc />
    public async Task<Result<EventAcceptedDto>> SubmitAsync(
        GameId gameId, UserId userId, SubmitEventRequest request, bool allowOverride, string? overrideReason, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(gameId, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        // Idempotent replay: an already-recorded eventId returns its ORIGINAL result, byte-identical,
        // because the state is re-derived at that event's sequence rather than "now".
        if (await _events.GetByIdAsync(gameId, request.EventId, ct) is { } existing)
        {
            return await ReplayAsync(game, existing, ct);
        }

        if (await DetectConflictAsync(game, request.LastKnownSequence, ct) is { } conflict)
        {
            return conflict;
        }

        return await SubmitInternalAsync(game, userId, [request], allowOverride, overrideReason, ct);
    }

    /// <inheritdoc />
    public async Task<Result<EventAcceptedDto>> SubmitBatchAsync(
        GameId gameId, UserId userId, SubmitEventBatchRequest request, bool allowOverride, string? overrideReason, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(gameId, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        if (request.Events.Count == 0)
        {
            return Error.Validation("EMPTY_BATCH", "A batch must contain at least one event.");
        }

        // If every event in the batch is already recorded, replay the last one idempotently.
        var known = new List<GameEvent>();
        foreach (var e in request.Events)
        {
            if (await _events.GetByIdAsync(gameId, e.EventId, ct) is { } found)
            {
                known.Add(found);
            }
        }

        if (known.Count == request.Events.Count)
        {
            return await ReplayAsync(game, known.OrderBy(e => e.Sequence).Last(), ct);
        }

        if (await DetectConflictAsync(game, request.LastKnownSequence, ct) is { } conflict)
        {
            return conflict;
        }

        return await SubmitInternalAsync(game, userId, request.Events, allowOverride, overrideReason, ct);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<GameEventDto>>> ListEventsAsync(GameId gameId, long? sinceSequence, CancellationToken ct = default)
    {
        var events = await _events.ListForGameAsync(gameId, sinceSequence, ct);
        return Result.Success<IReadOnlyList<GameEventDto>>(events.Select(Mappers.ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<EventAcceptedDto>> VoidAsync(
        GameId gameId, UserId userId, Guid targetEventId, VoidEventRequest request, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(gameId, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        var target = await _events.GetByIdAsync(gameId, targetEventId, ct);
        if (target is null)
        {
            return Error.NotFound("EVENT_NOT_FOUND", "The event does not exist in this game.");
        }

        if (target.IsVoided)
        {
            return Error.Conflict("EVENT_ALREADY_VOIDED", "That event has already been voided.");
        }

        return await AppendVoidAsync(game, userId, target, request.Reason, ct);
    }

    /// <inheritdoc />
    public async Task<Result<EventAcceptedDto>> UndoLastAsync(GameId gameId, UserId userId, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(gameId, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        var events = await _events.ListForGameAsync(gameId, null, ct);

        // Skip events that are already voided, and the VOID events themselves.
        var target = events
            .Where(e => !e.IsVoided && e.EventType != EventTypes.Void)
            .OrderByDescending(e => e.Sequence)
            .FirstOrDefault();

        if (target is null)
        {
            return Error.Conflict("NOTHING_TO_UNDO", "There is no event left to undo.");
        }

        return await AppendVoidAsync(game, userId, target, "undo-last", ct);
    }

    /// <inheritdoc />
    public async Task<Result<LiveGameStateDto>> GetStateAsync(GameId gameId, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(gameId, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        var (context, events) = await LoadAsync(game, ct);
        return Mappers.ToDto(_projector.Project(context, events).LiveState);
    }

    /// <inheritdoc />
    public async Task<Result<BoxScoreDto>> GetBoxScoreAsync(GameId gameId, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(gameId, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        var (context, events) = await LoadAsync(game, ct);
        var projection = _projector.Project(context, events);
        return new BoxScoreDto(gameId,
            projection.TeamStatlines.Select(Mappers.ToDto).ToList(),
            projection.PlayerStatlines.Select(Mappers.ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<SequenceConflict>> GetConflictAsync(GameId gameId, long lastKnownSequence, CancellationToken ct = default)
    {
        var head = await _events.GetMaxSequenceAsync(gameId, ct);
        var missing = await _events.ListForGameAsync(gameId, lastKnownSequence, ct);
        return new SequenceConflict(head, missing.Select(Mappers.ToDto).ToList());
    }

    // ── internals ────────────────────────────────────────────────────────────

    private async Task<Result<EventAcceptedDto>> SubmitInternalAsync(
        Game game, UserId userId, IReadOnlyList<SubmitEventRequest> requests, bool allowOverride, string? overrideReason, CancellationToken ct)
    {
        using var activity = RecordingTelemetry.StartSubmission(game.Id.Value, requests.Count);

        // Every log line on this path carries the game, so a failure at the scorer's table can be
        // traced without cross-referencing anything.
        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["gameId"] = game.Id.Value,
            ["userId"] = userId.Value,
        });

        var (context, existing) = await LoadAsync(game, ct);
        var log = existing.ToList();
        var sequence = log.Count == 0 ? 0 : log.Max(e => e.Sequence);
        var now = _clock.UtcNow;
        GameEvent? last = null;

        foreach (var request in requests)
        {
            if (!EventTypes.All.Contains(request.EventType))
            {
                _logger.LogWarning(
                    "Rejected unknown event type {EventType} at sequence {Sequence} for event {EventId}.",
                    request.EventType, sequence + 1, request.EventId);
                RecordingTelemetry.RecordOutcome(activity, "rejected", ruleCode: "UNKNOWN_EVENT_TYPE");
                return Error.Validation("UNKNOWN_EVENT_TYPE", $"'{request.EventType}' is not a recognised event type.");
            }

            var state = _projector.Project(context, log).LiveState;
            var candidate = GameEvent.Record(
                request.EventId, game.OrganisationId, game.Id, ++sequence, request.EventType, request.EventSubtype,
                request.Period, request.GameClockMs, now, userId, request.ShotClockMs, request.CompetitionTeamId,
                request.GameRosterEntryId, request.SecondaryRosterEntryId, request.Points,
                request.ShotXCm, request.ShotYCm, request.ClientRecordedAt, request.Payload);

            var validation = _pipeline.Evaluate(
                new ValidationContext(context, state, game.Status, LastNonVoided(log), FreeThrowsOwed(log)),
                candidate, allowOverride);

            if (!validation.IsAccepted)
            {
                var failure = validation.Failure!;

                // Enough context to reconstruct exactly what the official tapped: the rule that
                // rejected it, the event's identity and position, and who and what it referred to.
                _logger.LogWarning(
                    "Rejected {EventType}/{EventSubtype} at sequence {Sequence} by rule {RuleCode}: {RuleMessage}. "
                    + "eventId={EventId} period={Period} clockMs={GameClockMs} team={CompetitionTeamId} "
                    + "actor={GameRosterEntryId} secondary={SecondaryRosterEntryId}",
                    candidate.EventType, candidate.EventSubtype ?? "-", candidate.Sequence, failure.Code,
                    failure.Message, candidate.Id, candidate.Period, candidate.GameClockMs,
                    candidate.CompetitionTeamId, candidate.GameRosterEntryId, candidate.SecondaryRosterEntryId);

                RecordingTelemetry.RecordOutcome(activity, "rejected", candidate.Sequence, failure.Code);
                return failure.Tier == RuleTier.Overridable
                    ? Error.Validation(failure.Code!, $"{failure.Message} Submit with override=true and a reason to record it anyway.")
                    : Error.Validation(failure.Code!, failure.Message!);
            }

            if (validation.WasOverridden)
            {
                candidate.MarkOverridden(validation.Failure!.Code!, overrideReason);
                _logger.LogWarning(
                    "Recorded {EventType} at sequence {Sequence} OVERRIDING rule {RuleCode}. "
                    + "eventId={EventId} reason={OverrideReason}",
                    candidate.EventType, candidate.Sequence, validation.Failure.Code, candidate.Id,
                    overrideReason ?? "(none given)");
            }

            _events.Add(candidate);
            log.Add(candidate);
            last = candidate;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var projection = _projector.Project(context, log);
        RecordingTelemetry.RecordOutcome(activity, "accepted", last!.Sequence);
        _logger.LogInformation(
            "Recorded {EventCount} event(s), now at sequence {Sequence}.", requests.Count, last.Sequence);

        return new EventAcceptedDto(last.Sequence, Mappers.ToDto(last), Mappers.ToDto(projection.LiveState));
    }

    private async Task<Result<EventAcceptedDto>> AppendVoidAsync(
        Game game, UserId userId, GameEvent target, string reason, CancellationToken ct)
    {
        var (context, existing) = await LoadAsync(game, ct);
        var log = existing.ToList();
        var sequence = (log.Count == 0 ? 0 : log.Max(e => e.Sequence)) + 1;

        var voidEvent = GameEvent.Record(
            Guid.CreateVersion7(), game.OrganisationId, game.Id, sequence, EventTypes.Void, null,
            target.Period, target.GameClockMs, _clock.UtcNow, userId,
            payload: new Dictionary<string, string> { ["targetEventId"] = target.Id.ToString(), ["reason"] = reason });

        // The target is flagged in the same transaction; its row is never removed (non-negotiable #1).
        target.Void();
        _events.Add(voidEvent);
        await _unitOfWork.SaveChangesAsync(ct);

        log.Add(voidEvent);
        var projection = _projector.Project(context, log);
        return new EventAcceptedDto(voidEvent.Sequence, Mappers.ToDto(voidEvent), Mappers.ToDto(projection.LiveState));
    }

    /// <summary>Re-derives the original response for an already-recorded event, deterministically.</summary>
    private async Task<Result<EventAcceptedDto>> ReplayAsync(Game game, GameEvent existing, CancellationToken ct)
    {
        var (context, all) = await LoadAsync(game, ct);
        var upToThatPoint = all.Where(e => e.Sequence <= existing.Sequence).ToList();
        var projection = _projector.Project(context, upToThatPoint);
        return new EventAcceptedDto(existing.Sequence, Mappers.ToDto(existing), Mappers.ToDto(projection.LiveState));
    }

    private async Task<Result<EventAcceptedDto>?> DetectConflictAsync(Game game, long? lastKnownSequence, CancellationToken ct)
    {
        if (lastKnownSequence is not { } known)
        {
            return null;
        }

        var head = await _events.GetMaxSequenceAsync(game.Id, ct);
        if (known >= head)
        {
            return null;
        }

        var missing = await _events.ListForGameAsync(game.Id, known, ct);
        var detail = string.Join(",", missing.Select(m => m.Sequence));
        return Error.Conflict("STALE_SEQUENCE",
            $"The log has moved on: current sequence is {head}, you last saw {known}. Missing sequences: {detail}");
    }

    private async Task<(GameContext Context, IReadOnlyList<GameEvent> Events)> LoadAsync(Game game, CancellationToken ct)
    {
        var roster = await _rosters.ListForGameAsync(game.Id, ct);
        var events = await _events.ListForGameAsync(game.Id, null, ct);

        var context = new GameContext(
            game.Id, game.HomeCompetitionTeamId, game.AwayCompetitionTeamId,
            roster.Select(r => new GamePlayer(r.Id, r.CompetitionTeamId, r.PlayerId, r.IsStarter)).ToList(),
            game.RuleSetSnapshot ?? SharedKernel.RuleSet.Fiba());

        return (context, events);
    }

    private static GameEvent? LastNonVoided(IReadOnlyList<GameEvent> log)
        => log.Where(e => !e.IsVoided && e.EventType != EventTypes.Void).OrderByDescending(e => e.Sequence).FirstOrDefault();

    /// <summary>Free throws still owed from the most recent shooting/technical foul.</summary>
    private static int FreeThrowsOwed(IReadOnlyList<GameEvent> log)
    {
        var foul = log.Where(e => !e.IsVoided && e.EventType == EventTypes.Foul)
            .OrderByDescending(e => e.Sequence).FirstOrDefault();
        if (foul is null)
        {
            return 0;
        }

        var awarded = foul.Payload.TryGetValue("freeThrowsAwarded", out var raw) && int.TryParse(raw, out var n) ? n : 0;
        var taken = log.Count(e => !e.IsVoided && e.Sequence > foul.Sequence
            && e.EventType is EventTypes.FreeThrowMade or EventTypes.FreeThrowMissed);
        return Math.Max(0, awarded - taken);
    }

    private static Error GameNotFound() => Error.NotFound("GAME_NOT_FOUND", "The game does not exist.");
}
