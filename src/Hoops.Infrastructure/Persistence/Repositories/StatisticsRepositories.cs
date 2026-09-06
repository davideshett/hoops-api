using Hoops.Modules.Statistics.Application.Abstractions;
using Hoops.Modules.Statistics.Domain;
using Hoops.SharedKernel.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace Hoops.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF-backed <see cref="IStatisticsRepository"/> over the derived statistics tables.
///
/// The recompute path runs as a SYSTEM operation — from an admin command or the background outbox
/// drainer — where there is no ambient tenant. Those reads and deletes therefore bypass the global
/// tenant filter deliberately: they are already scoped precisely by an explicit game, competition, or
/// player id, and relying on the ambient tenant would silently rebuild nothing. Rows are still WRITTEN
/// with the organisation id taken from the game, so tenant-scoped API reads keep working normally.
/// </summary>
public sealed class StatisticsRepository(AppDbContext db) : IStatisticsRepository
{
    /// <inheritdoc />
    public async Task DeleteGameStatisticsAsync(GameId gameId, CancellationToken ct = default)
    {
        // ExecuteDelete issues one DELETE per table rather than loading rows to delete them, which
        // matters when a full-competition recompute clears hundreds of games.
        await db.PlayerGameStatlines.IgnoreQueryFilters().Where(s => s.GameId == gameId).ExecuteDeleteAsync(ct);
        await db.TeamGameStatlines.IgnoreQueryFilters().Where(s => s.GameId == gameId).ExecuteDeleteAsync(ct);
        await db.GamePeriodStates.IgnoreQueryFilters().Where(s => s.GameId == gameId).ExecuteDeleteAsync(ct);
        await db.LineupStints.IgnoreQueryFilters().Where(s => s.GameId == gameId).ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteCompetitionGameStatisticsAsync(CompetitionId competitionId, CancellationToken ct = default)
    {
        var gameIds = db.PlayerGameStatlines.IgnoreQueryFilters()
            .Where(s => s.CompetitionId == competitionId).Select(s => s.GameId)
            .Union(db.TeamGameStatlines.IgnoreQueryFilters()
                .Where(s => s.CompetitionId == competitionId).Select(s => s.GameId));

        await db.GamePeriodStates.IgnoreQueryFilters().Where(s => gameIds.Contains(s.GameId)).ExecuteDeleteAsync(ct);
        await db.LineupStints.IgnoreQueryFilters().Where(s => gameIds.Contains(s.GameId)).ExecuteDeleteAsync(ct);
        await db.PlayerGameStatlines.IgnoreQueryFilters().Where(s => s.CompetitionId == competitionId).ExecuteDeleteAsync(ct);
        await db.TeamGameStatlines.IgnoreQueryFilters().Where(s => s.CompetitionId == competitionId).ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    public void AddGameStatistics(
        IEnumerable<PlayerGameStatline> players, IEnumerable<TeamGameStatline> teams,
        IEnumerable<GamePeriodStateRow> periods, IEnumerable<LineupStintRow> stints)
    {
        db.PlayerGameStatlines.AddRange(players);
        db.TeamGameStatlines.AddRange(teams);
        db.GamePeriodStates.AddRange(periods);
        db.LineupStints.AddRange(stints);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlayerGameStatline>> ListPlayerStatlinesForCompetitionAsync(
        CompetitionId competitionId, CancellationToken ct = default)
        => await db.PlayerGameStatlines.IgnoreQueryFilters().Where(s => s.CompetitionId == competitionId).ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeamGameStatline>> ListTeamStatlinesForCompetitionAsync(
        CompetitionId competitionId, CancellationToken ct = default)
        => await db.TeamGameStatlines.IgnoreQueryFilters().Where(s => s.CompetitionId == competitionId).ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlayerGameStatline>> ListPlayerStatlinesForPlayersAsync(
        IReadOnlyCollection<PlayerId> playerIds, CancellationToken ct = default)
        // Careers span organisations, so this read deliberately ignores the tenant filter.
        => await db.PlayerGameStatlines.IgnoreQueryFilters()
            .Where(s => playerIds.Contains(s.PlayerId)).ToListAsync(ct);

    /// <inheritdoc />
    public async Task DeleteCompetitionAggregatesAsync(CompetitionId competitionId, CancellationToken ct = default)
        => await db.CompetitionPlayerAggregates.IgnoreQueryFilters().Where(a => a.CompetitionId == competitionId).ExecuteDeleteAsync(ct);

    /// <inheritdoc />
    public void AddCompetitionAggregates(IEnumerable<CompetitionPlayerAggregate> aggregates)
        => db.CompetitionPlayerAggregates.AddRange(aggregates);

    /// <inheritdoc />
    public async Task DeleteCareerAggregatesAsync(IReadOnlyCollection<PlayerId> playerIds, CancellationToken ct = default)
        => await db.PlayerCareerAggregates.Where(a => playerIds.Contains(a.PlayerId)).ExecuteDeleteAsync(ct);

    /// <inheritdoc />
    public void AddCareerAggregates(IEnumerable<PlayerCareerAggregate> aggregates)
        => db.PlayerCareerAggregates.AddRange(aggregates);

    /// <inheritdoc />
    public async Task DeleteStandingsAsync(CompetitionId competitionId, CancellationToken ct = default)
        => await db.CompetitionStandings.IgnoreQueryFilters().Where(s => s.CompetitionId == competitionId).ExecuteDeleteAsync(ct);

    /// <inheritdoc />
    public void AddStandings(IEnumerable<CompetitionStanding> standings) => db.CompetitionStandings.AddRange(standings);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompetitionStanding>> ListStandingsAsync(
        CompetitionId competitionId, CancellationToken ct = default)
        => await db.CompetitionStandings.Where(s => s.CompetitionId == competitionId)
            .OrderBy(s => s.Position).ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompetitionPlayerAggregate>> ListCompetitionAggregatesAsync(
        CompetitionId competitionId, CancellationToken ct = default)
        => await db.CompetitionPlayerAggregates.Where(a => a.CompetitionId == competitionId).ToListAsync(ct);

    /// <inheritdoc />
    public Task<PlayerCareerAggregate?> GetCareerAggregateAsync(PlayerId playerId, CancellationToken ct = default)
        => db.PlayerCareerAggregates.FirstOrDefaultAsync(a => a.PlayerId == playerId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlayerGameStatline>> ListPlayerStatlinesForGameAsync(
        GameId gameId, CancellationToken ct = default)
        => await db.PlayerGameStatlines.IgnoreQueryFilters().Where(s => s.GameId == gameId).ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeamGameStatline>> ListTeamStatlinesForGameAsync(
        GameId gameId, CancellationToken ct = default)
        => await db.TeamGameStatlines.IgnoreQueryFilters().Where(s => s.GameId == gameId).ToListAsync(ct);
}

/// <summary>EF-backed <see cref="IOutboxRepository"/>.</summary>
public sealed class OutboxRepository(AppDbContext db) : IOutboxRepository
{
    /// <inheritdoc />
    public void Add(OutboxMessage message) => db.OutboxMessages.Add(message);

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> ListPendingAsync(int limit, CancellationToken ct = default)
        => await db.OutboxMessages.Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt).Take(limit).ToListAsync(ct);
}
