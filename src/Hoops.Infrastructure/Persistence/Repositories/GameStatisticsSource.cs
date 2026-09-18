using Hoops.Modules.GameRecording.Domain;
using Hoops.Modules.GameRecording.Domain.Projection;
using Hoops.Modules.Statistics.Application.Abstractions;
using Hoops.SharedKernel;
using Hoops.SharedKernel.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace Hoops.Infrastructure.Persistence.Repositories;

/// <summary>
/// Bridges the Statistics module to the game event log. Lives in Infrastructure precisely so the
/// Statistics module never references GameRecording's persistence — it asks for facts and a
/// projection, and gets them.
/// </summary>
public sealed class GameStatisticsSource(AppDbContext db, IGameProjector projector) : IGameStatisticsSource
{
    // A forfeited game has an awarded result rather than a played one, so it is finalised for
    // standings purposes but produces no statlines.
    private static readonly GameStatus[] CountingStatuses = [GameStatus.Finalized];

    /// <inheritdoc />
    public async Task<GameFacts?> GetFactsAsync(GameId gameId, CancellationToken ct = default)
    {
        var game = await db.Games.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.Id == gameId, ct);
        return game is null ? null : ToFacts(game);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GameFacts>> ListFinalizedForCompetitionAsync(
        CompetitionId competitionId, CancellationToken ct = default)
    {
        var games = await db.Games.IgnoreQueryFilters()
            .Where(g => g.CompetitionId == competitionId && CountingStatuses.Contains(g.Status))
            .ToListAsync(ct);
        return games.Select(ToFacts).ToList();
    }

    /// <inheritdoc />
    public async Task<OrganisationId?> GetCompetitionOrganisationAsync(
        CompetitionId competitionId, CancellationToken ct = default)
        => await db.Competitions.IgnoreQueryFilters()
            .Where(c => c.Id == competitionId)
            .Select(c => (OrganisationId?)c.OrganisationId)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<int> CountScheduledGamesAsync(CompetitionId competitionId, CancellationToken ct = default)
        => db.Games.IgnoreQueryFilters()
            .CountAsync(g => g.CompetitionId == competitionId && g.Status != GameStatus.Cancelled, ct);

    /// <inheritdoc />
    public async Task<ProjectedGame?> ProjectAsync(GameId gameId, CancellationToken ct = default)
    {
        var game = await db.Games.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.Id == gameId, ct);
        if (game?.RuleSetSnapshot is null)
        {
            return null;
        }

        var roster = await db.GameRosterEntries.IgnoreQueryFilters()
            .Where(r => r.GameId == gameId).ToListAsync(ct);
        var events = await db.GameEvents.IgnoreQueryFilters()
            .Where(e => e.GameId == gameId).OrderBy(e => e.Sequence).ToListAsync(ct);

        // The game roster snapshot stays frozen forever — it is the record of who was on the sheet.
        // But statistics must follow a MERGED player onto the surviving record, or a recompute would
        // silently undo every merge. So the snapshot is read as-is and the player id resolved through
        // the merge chain here, on the write path (§5A.5).
        var survivors = await ResolveMergesAsync(roster.Select(r => r.PlayerId).Distinct().ToList(), ct);

        var context = new GameContext(
            game.Id,
            game.HomeCompetitionTeamId,
            game.AwayCompetitionTeamId,
            roster.Select(r => new GamePlayer(
                r.Id, r.CompetitionTeamId, survivors.GetValueOrDefault(r.PlayerId, r.PlayerId), r.IsStarter)).ToList(),
            game.RuleSetSnapshot);

        // The same pure projector the live path uses — persisted statistics and the live box score can
        // never disagree, because they are the same function over the same log.
        var projection = projector.Project(context, events);

        return new ProjectedGame(
            projection.PlayerStatlines.Select(p => new ProjectedPlayerLine(
                p.GameRosterEntryId, p.PlayerId, p.CompetitionTeamId, p.Points, p.FieldGoalsMade,
                p.FieldGoalsAttempted, p.ThreePointersMade, p.ThreePointersAttempted, p.FreeThrowsMade,
                p.FreeThrowsAttempted, p.OffensiveRebounds, p.DefensiveRebounds, p.Assists, p.Steals,
                p.Blocks, p.BlocksAgainst, p.Turnovers, p.FoulsCommitted, p.FoulsDrawn, p.FouledOut,
                p.SecondsPlayed, p.PlusMinus)).ToList(),
            projection.TeamStatlines.Select(t => new ProjectedTeamLine(
                t.CompetitionTeamId, t.Points, t.FieldGoalsMade, t.FieldGoalsAttempted, t.ThreePointersMade,
                t.ThreePointersAttempted, t.FreeThrowsMade, t.FreeThrowsAttempted, t.OffensiveRebounds,
                t.DefensiveRebounds, t.Assists, t.Steals, t.Blocks, t.Turnovers, t.FoulsCommitted)).ToList(),
            projection.Periods.SelectMany(period => period.PointsByTeam.Select(kv => new ProjectedPeriodLine(
                kv.Key, period.Period, kv.Value, period.TeamFoulsByTeam.GetValueOrDefault(kv.Key)))).ToList(),
            projection.LineupStints.Select(s => new ProjectedStint(
                s.CompetitionTeamId, s.Period, string.Join(',', s.Players.Select(p => p.Value)),
                s.SecondsPlayed, s.PointsFor, s.PointsAgainst)).ToList());
    }

    /// <summary>Maps each player id to the record it has ultimately been merged into.</summary>
    private async Task<Dictionary<PlayerId, PlayerId>> ResolveMergesAsync(
        IReadOnlyList<PlayerId> playerIds, CancellationToken ct)
    {
        var resolved = new Dictionary<PlayerId, PlayerId>();
        var merged = await db.Players.IgnoreQueryFilters()
            .Where(p => playerIds.Contains(p.Id) && p.MergedIntoId != null)
            .Select(p => new { p.Id, p.MergedIntoId })
            .ToListAsync(ct);

        foreach (var row in merged)
        {
            // Merges resolve transitively on write, so one hop is normally enough; the loop guards
            // against a chain written before that guarantee held.
            var target = row.MergedIntoId!.Value;
            var guard = 0;
            while (guard++ < 10)
            {
                var next = await db.Players.IgnoreQueryFilters()
                    .Where(p => p.Id == target).Select(p => p.MergedIntoId).FirstOrDefaultAsync(ct);
                if (next is null)
                {
                    break;
                }

                target = next.Value;
            }

            resolved[row.Id] = target;
        }

        return resolved;
    }

    private static GameFacts ToFacts(Game game) => new(
        game.Id, game.OrganisationId, game.CompetitionId, game.HomeCompetitionTeamId,
        game.AwayCompetitionTeamId, game.GroupId,
        game.RuleSetSnapshot ?? RuleSet.Fiba(),
        game.Status == GameStatus.Finalized);
}
