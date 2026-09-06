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

        var context = new GameContext(
            game.Id,
            game.HomeCompetitionTeamId,
            game.AwayCompetitionTeamId,
            roster.Select(r => new GamePlayer(r.Id, r.CompetitionTeamId, r.PlayerId, r.IsStarter)).ToList(),
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

    private static GameFacts ToFacts(Game game) => new(
        game.Id, game.OrganisationId, game.CompetitionId, game.HomeCompetitionTeamId,
        game.AwayCompetitionTeamId, game.GroupId,
        game.RuleSetSnapshot ?? RuleSet.Fiba(),
        game.Status == GameStatus.Finalized);
}
