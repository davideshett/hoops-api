using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.GameRecording.Contracts;

/// <summary>A stale-sequence conflict: what the client missed, so it can self-heal.</summary>
/// <param name="CurrentSequence">The log's current head.</param>
/// <param name="MissingEvents">Every event after the client's last known sequence.</param>
public sealed record SequenceConflict(long CurrentSequence, IReadOnlyList<GameEventDto> MissingEvents);

/// <summary>Event recording: submission with idempotency and conflict detection, void/undo, live state.</summary>
public interface IEventRecordingService
{
    /// <summary>Starts the game (RosterLocked → InProgress).</summary>
    Task<Result<LiveGameStateDto>> StartGameAsync(GameId gameId, UserId userId, CancellationToken ct = default);

    /// <summary>Ends the game (InProgress → PendingReview).</summary>
    Task<Result<LiveGameStateDto>> EndGameAsync(GameId gameId, UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Submits one event. Replaying an <c>eventId</c> already recorded returns the original result
    /// unchanged; a stale <c>lastKnownSequence</c> yields a conflict carrying the missing events.
    /// </summary>
    Task<Result<EventAcceptedDto>> SubmitAsync(
        GameId gameId, UserId userId, SubmitEventRequest request, bool allowOverride, string? overrideReason,
        CancellationToken ct = default);

    /// <summary>Submits an ordered batch, all-or-nothing.</summary>
    Task<Result<EventAcceptedDto>> SubmitBatchAsync(
        GameId gameId, UserId userId, SubmitEventBatchRequest request, bool allowOverride, string? overrideReason,
        CancellationToken ct = default);

    /// <summary>Lists a game's events, optionally only those after <paramref name="sinceSequence"/>.</summary>
    Task<Result<IReadOnlyList<GameEventDto>>> ListEventsAsync(GameId gameId, long? sinceSequence, CancellationToken ct = default);

    /// <summary>Voids one event by appending a VOID event; the target row itself is retained.</summary>
    Task<Result<EventAcceptedDto>> VoidAsync(GameId gameId, UserId userId, Guid targetEventId, VoidEventRequest request, CancellationToken ct = default);

    /// <summary>Voids the highest non-voided event, skipping ones already voided.</summary>
    Task<Result<EventAcceptedDto>> UndoLastAsync(GameId gameId, UserId userId, CancellationToken ct = default);

    /// <summary>The live state, derived by replaying the log.</summary>
    Task<Result<LiveGameStateDto>> GetStateAsync(GameId gameId, CancellationToken ct = default);

    /// <summary>The box score, derived by replaying the log.</summary>
    Task<Result<BoxScoreDto>> GetBoxScoreAsync(GameId gameId, CancellationToken ct = default);

    /// <summary>The conflict payload for a stale sequence, if any.</summary>
    Task<Result<SequenceConflict>> GetConflictAsync(GameId gameId, long lastKnownSequence, CancellationToken ct = default);
}
