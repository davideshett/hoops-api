using System.Text.Json;
using Hoops.Modules.GameRecording.Application.Abstractions;
using Hoops.Modules.GameRecording.Contracts;
using Hoops.Modules.GameRecording.Domain;
using Hoops.Modules.Statistics.Application.Abstractions;
using Hoops.Modules.Statistics.Domain;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.GameRecording.Application;

/// <summary>
/// The gated transitions that decide whether a game counts (ADR-006): finalise, reopen for amendment,
/// and forfeit. Each writes an outbox message in the SAME transaction as the status change, so the
/// recomputation that follows can never be lost to a crash (§9.3).
/// </summary>
public sealed class GameFinalizationService : IGameFinalizationService
{
    private readonly IGameRepository _games;
    private readonly IOutboxRepository _outbox;
    private readonly IGameRecordingUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the service.</summary>
    public GameFinalizationService(
        IGameRepository games, IOutboxRepository outbox, IGameRecordingUnitOfWork unitOfWork, IClock clock)
    {
        _games = games;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Result<GameDto>> FinalizeAsync(GameId gameId, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(gameId, ct);
        if (game is null)
        {
            return NotFound();
        }

        if (!game.Finalize(_clock.UtcNow))
        {
            return Error.Conflict("INVALID_STATUS_TRANSITION",
                $"A game in {game.Status} cannot be finalised; it must be in PendingReview.");
        }

        EnqueueRecompute(game, "GameFinalized");
        await _unitOfWork.SaveChangesAsync(ct);
        return game.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<GameDto>> ReopenAsync(GameId gameId, ReopenGameRequest request, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(gameId, ct);
        if (game is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Error.Validation("REASON_REQUIRED", "Reopening a finalised game requires a reason.");
        }

        if (!game.Reopen(request.Reason))
        {
            return Error.Conflict("INVALID_STATUS_TRANSITION",
                $"A game in {game.Status} cannot be reopened; it must be Finalized.");
        }

        // The game no longer counts, so its statistics must be withdrawn from every aggregate that
        // included them — the recompute below does exactly that.
        EnqueueRecompute(game, "GameReopened");
        await _unitOfWork.SaveChangesAsync(ct);
        return game.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<GameDto>> ForfeitAsync(GameId gameId, ForfeitGameRequest request, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(gameId, ct);
        if (game is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Error.Validation("REASON_REQUIRED", "A forfeit requires a reason.");
        }

        if (!game.Forfeit(request.WinningCompetitionTeamId, request.Reason, _clock.UtcNow))
        {
            return Error.Conflict("INVALID_FORFEIT",
                "The winning team must be one of the two teams, and the game must not already be final or cancelled.");
        }

        EnqueueRecompute(game, "GameForfeited");
        await _unitOfWork.SaveChangesAsync(ct);
        return game.ToDto();
    }

    private void EnqueueRecompute(Game game, string messageType)
    {
        var payload = JsonSerializer.Serialize(new
        {
            gameId = game.Id.Value,
            competitionId = game.CompetitionId.Value,
        });
        _outbox.Add(OutboxMessage.Enqueue(messageType, payload, _clock.UtcNow));
    }

    private static Error NotFound() => Error.NotFound("GAME_NOT_FOUND", "The game does not exist.");
}
