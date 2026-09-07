using Hoops.Modules.Registry.Application.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace Hoops.Infrastructure.Persistence.Repositories;

/// <summary>
/// Moves persisted statlines onto a surviving player when records merge (§5A.5). Resolving the merge
/// on WRITE like this keeps every later read — career pages, leaderboards, records — a plain lookup
/// with no merge-chain resolution.
/// </summary>
public sealed class PlayerStatisticsRepointer(AppDbContext db) : IPlayerStatisticsRepointer
{
    /// <inheritdoc />
    public async Task<int> RepointStatlinesAsync(
        IReadOnlyCollection<PlayerId> sourcePlayerIds, PlayerId survivorId, CancellationToken ct = default)
    {
        if (sourcePlayerIds.Count == 0)
        {
            return 0;
        }

        // A merge is a platform-admin operation spanning organisations, so it deliberately bypasses the
        // tenant filter; the rows are addressed by explicit player id.
        var moved = await db.PlayerGameStatlines.IgnoreQueryFilters()
            .Where(s => sourcePlayerIds.Contains(s.PlayerId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.PlayerId, survivorId), ct);

        // The losing records' career rows are meaningless now; the survivor's is rebuilt by recompute.
        await db.PlayerCareerAggregates.IgnoreQueryFilters()
            .Where(a => sourcePlayerIds.Contains(a.PlayerId)).ExecuteDeleteAsync(ct);

        return moved;
    }
}
