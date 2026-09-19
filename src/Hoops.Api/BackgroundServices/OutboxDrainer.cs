using System.Text.Json;
using Hoops.Modules.Statistics.Application.Abstractions;
using Hoops.Modules.Statistics.Contracts;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Api.BackgroundServices;

/// <summary>
/// Drains the transactional outbox (§9.3). Messages are written in the same transaction as the state
/// change that produced them, so recomputation survives a crash between "game finalised" and
/// "aggregates rebuilt". Failed messages stay pending and are retried on the next pass.
/// </summary>
public sealed class OutboxDrainer : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDrainer> _logger;

    /// <summary>Creates the drainer.</summary>
    public OutboxDrainer(IServiceScopeFactory scopeFactory, ILogger<OutboxDrainer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A drain failure must never take the host down; the messages remain pending.
                _logger.LogError(ex, "Outbox drain pass failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                break;
            }
        }
    }

    /// <summary>Processes one batch. Exposed so tests can drain deterministically instead of waiting.</summary>
    public async Task<int> DrainOnceAsync(CancellationToken ct)
    {
        var processed = 0;

        // One message per transaction: the claim (FOR UPDATE SKIP LOCKED), the recompute, and the
        // mark commit together, so a crash mid-recompute leaves the message pending and the
        // statistics untouched, and two drainers never work the same message.
        for (var i = 0; i < BatchSize; i++)
        {
            using var scope = _scopeFactory.CreateScope();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            var recompute = scope.ServiceProvider.GetRequiredService<IStatisticsRecomputeService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IStatisticsUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            await unitOfWork.BeginAsync(ct);
            var message = await outbox.ClaimNextPendingAsync(ct);
            if (message is null)
            {
                await unitOfWork.RollbackAsync(ct);
                break;
            }

            try
            {
                var competitionId = ReadCompetitionId(message.Payload);
                if (competitionId is { } id)
                {
                    // A system caller: the drainer has no ambient tenant, and the message was
                    // written by an already-authorised state change.
                    var result = await recompute.RecomputeCompetitionAsync(id, null, ct);
                    if (result.IsFailure)
                    {
                        message.MarkFailed(result.Error.Code);
                        await unitOfWork.SaveChangesAsync(ct);
                        await unitOfWork.CommitAsync(ct);
                        continue;
                    }
                }

                message.MarkProcessed(clock.UtcNow);
                await unitOfWork.SaveChangesAsync(ct);
                await unitOfWork.CommitAsync(ct);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {MessageId}.", message.Id);
                await unitOfWork.RollbackAsync(ct);

                // Record the failure in its own transaction so the message is not retried forever.
                using var failureScope = _scopeFactory.CreateScope();
                var failedOutbox = failureScope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                var failedUnitOfWork = failureScope.ServiceProvider.GetRequiredService<IStatisticsUnitOfWork>();
                var failed = await failedOutbox.GetAsync(message.Id, ct);
                failed?.MarkFailed(ex.Message);
                await failedUnitOfWork.SaveChangesAsync(ct);
            }
        }

        return processed;
    }

    private static CompetitionId? ReadCompetitionId(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.TryGetProperty("competitionId", out var value)
            && Guid.TryParse(value.GetString(), out var id)
                ? CompetitionId.FromGuid(id)
                : null;
    }
}
