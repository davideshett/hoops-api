using Hoops.Modules.Statistics.Application.Abstractions;
using Hoops.Modules.Statistics.Contracts;
using Hoops.Modules.Statistics.Domain;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Statistics.Application;

/// <summary>
/// Rebuilds every derived statistic from the event log (ADR-001). Because these tables are only ever
/// written here, and only ever from a replay, "truncate and recompute" is guaranteed to reproduce
/// exactly what the API served before — which is what makes the event log the source of truth in
/// practice, not just in principle.
/// </summary>
public sealed class StatisticsRecomputeService : IStatisticsRecomputeService
{
    private readonly IGameStatisticsSource _games;
    private readonly IStatisticsRepository _repository;
    private readonly IStatisticsUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the service.</summary>
    public StatisticsRecomputeService(
        IGameStatisticsSource games, IStatisticsRepository repository,
        IStatisticsUnitOfWork unitOfWork, IClock clock)
    {
        _games = games;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Result<RecomputeSummaryDto>> RecomputeGameAsync(
        GameId gameId, OrganisationId? requiredOrganisationId, CancellationToken ct = default)
    {
        var facts = await _games.GetFactsAsync(gameId, ct);

        // Another organisation's game is reported as absent rather than forbidden, so this endpoint
        // cannot be used to probe which game ids exist elsewhere on the platform.
        if (facts is null || (requiredOrganisationId is { } org && facts.OrganisationId != org))
        {
            return Error.NotFound("GAME_NOT_FOUND", "The game does not exist.");
        }

        var written = await RebuildGameAsync(facts, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // A game's statistics feed its competition's aggregates and standings, so those follow. The
        // competition is the game's own, and the game is already authorised, so no further check.
        var competition = await RecomputeCompetitionAsync(facts.CompetitionId, null, ct);
        return competition.IsFailure
            ? competition.Error
            : new RecomputeSummaryDto(1, written, competition.Value.AggregatesWritten, competition.Value.StandingsRowsWritten);
    }

    /// <inheritdoc />
    public async Task<Result<RecomputeSummaryDto>> RecomputeCompetitionAsync(
        CompetitionId competitionId, OrganisationId? requiredOrganisationId, CancellationToken ct = default)
    {
        if (requiredOrganisationId is { } org)
        {
            // Checked against the competition itself, not its games, so an empty competition is
            // guarded too. Absent rather than forbidden, for the same reason as above.
            var owner = await _games.GetCompetitionOrganisationAsync(competitionId, ct);
            if (owner != org)
            {
                return Error.NotFound("COMPETITION_NOT_FOUND", "The competition does not exist.");
            }
        }

        // One transaction, one lock, for the whole delete-and-rebuild (see IStatisticsUnitOfWork).
        await _unitOfWork.BeginRecomputeAsync(competitionId, ct);
        var result = await RebuildCompetitionAsync(competitionId, ct);
        await _unitOfWork.CommitAsync(ct);
        return result;
    }

    private async Task<Result<RecomputeSummaryDto>> RebuildCompetitionAsync(CompetitionId competitionId, CancellationToken ct)
    {
        var games = await _games.ListFinalizedForCompetitionAsync(competitionId, ct);

        // 0. Clear the whole competition first. Rebuilding only the games that are CURRENTLY finalised
        // would leave stale statlines behind for any game that has since been reopened or forfeited,
        // and those rows would keep feeding the leaderboards.
        await _repository.DeleteCompetitionGameStatisticsAsync(competitionId, ct);

        // 1. Rebuild every finalised game's statlines from its log.
        var statlinesWritten = 0;
        foreach (var game in games)
        {
            statlinesWritten += await RebuildGameAsync(game, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // 2. Rebuild the competition's player aggregates from those statlines.
        var lines = await _repository.ListPlayerStatlinesForCompetitionAsync(competitionId, ct);
        await _repository.DeleteCompetitionAggregatesAsync(competitionId, ct);

        var scheduled = await _games.CountScheduledGamesAsync(competitionId, ct);
        var rules = games.FirstOrDefault()?.RuleSet;
        var threshold = rules is null
            ? 0
            : (int)Math.Ceiling(scheduled * rules.QualificationGamesFraction);

        var aggregates = lines
            .GroupBy(l => (l.OrganisationId, l.PlayerId))
            .Select(g => CompetitionPlayerAggregate.FromStatlines(g.Key.OrganisationId, competitionId, g.Key.PlayerId, g.ToList()))
            .ToList();

        // Qualification is recomputed for EVERY player, not just those in the game that triggered
        // this: the threshold moves as the competition progresses, so finalising one game can qualify
        // or disqualify players who were nowhere near it (§9.3).
        foreach (var aggregate in aggregates)
        {
            aggregate.SetQualified(threshold > 0 && aggregate.GamesPlayed >= threshold);
        }

        _repository.AddCompetitionAggregates(aggregates);

        // 3. Rebuild standings from the team statlines.
        var standingsRows = await RebuildStandingsAsync(competitionId, games, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        // 4. Careers span organisations, so rebuild them for every player touched here.
        var playerIds = lines.Select(l => l.PlayerId).Distinct().ToList();
        await RecomputeCareersAsync(playerIds, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new RecomputeSummaryDto(games.Count, statlinesWritten, aggregates.Count, standingsRows);
    }

    /// <inheritdoc />
    public async Task<Result<RecomputeSummaryDto>> RecomputeCareersAsync(
        IReadOnlyCollection<PlayerId> playerIds, CancellationToken ct = default)
    {
        if (playerIds.Count == 0)
        {
            return new RecomputeSummaryDto(0, 0, 0, 0);
        }

        var lines = await _repository.ListPlayerStatlinesForPlayersAsync(playerIds, ct);
        await _repository.DeleteCareerAggregatesAsync(playerIds, ct);

        var careers = lines
            .GroupBy(l => l.PlayerId)
            .Select(g => PlayerCareerAggregate.FromStatlines(g.Key, g.ToList()))
            .ToList();

        _repository.AddCareerAggregates(careers);
        await _unitOfWork.SaveChangesAsync(ct);
        return new RecomputeSummaryDto(0, 0, careers.Count, 0);
    }

    /// <summary>Replays one game and replaces its persisted statistics. Returns rows written.</summary>
    private async Task<int> RebuildGameAsync(GameFacts facts, CancellationToken ct)
    {
        // Always clear first: a game that is no longer finalised (reopened, cancelled) must leave no
        // statistics behind, or a reopened game would keep contributing to leaderboards.
        await _repository.DeleteGameStatisticsAsync(facts.GameId, ct);

        if (!facts.IsFinalized)
        {
            return 0;
        }

        var projected = await _games.ProjectAsync(facts.GameId, ct);
        if (projected is null)
        {
            return 0;
        }

        var teamPoints = projected.Teams.ToDictionary(t => t.CompetitionTeamId, t => t.Points);

        var players = projected.Players.Select(p => PlayerGameStatline.FromProjection(
            facts.OrganisationId, facts.GameId, facts.CompetitionId, p.CompetitionTeamId, p.PlayerId,
            p.GameRosterEntryId, p.Points, p.Fgm, p.Fga, p.Tpm, p.Tpa, p.Ftm, p.Fta, p.Oreb, p.Dreb,
            p.Assists, p.Steals, p.Blocks, p.BlocksAgainst, p.Turnovers, p.FoulsCommitted, p.FoulsDrawn,
            p.FouledOut, p.SecondsPlayed, p.PlusMinus)).ToList();

        var teams = projected.Teams.Select(t =>
        {
            var opponent = t.CompetitionTeamId == facts.HomeCompetitionTeamId
                ? facts.AwayCompetitionTeamId
                : facts.HomeCompetitionTeamId;
            return TeamGameStatline.FromProjection(
                facts.OrganisationId, facts.GameId, facts.CompetitionId, t.CompetitionTeamId, opponent,
                t.CompetitionTeamId == facts.HomeCompetitionTeamId, t.Points,
                teamPoints.GetValueOrDefault(opponent), t.Fgm, t.Fga, t.Tpm, t.Tpa, t.Ftm, t.Fta,
                t.Oreb, t.Dreb, t.Assists, t.Steals, t.Blocks, t.Turnovers, t.FoulsCommitted);
        }).ToList();

        var periods = projected.Periods.Select(p => GamePeriodStateRow.Create(
            facts.OrganisationId, facts.GameId, p.CompetitionTeamId, p.Period, p.Points, p.TeamFouls)).ToList();

        var stints = projected.Stints.Select(s => LineupStintRow.Create(
            facts.OrganisationId, facts.GameId, s.CompetitionTeamId, s.Period, s.PlayerIds,
            s.SecondsPlayed, s.PointsFor, s.PointsAgainst)).ToList();

        _repository.AddGameStatistics(players, teams, periods, stints);
        return players.Count + teams.Count + periods.Count + stints.Count;
    }

    private async Task<int> RebuildStandingsAsync(
        CompetitionId competitionId, IReadOnlyList<GameFacts> games, CancellationToken ct)
    {
        await _repository.DeleteStandingsAsync(competitionId, ct);
        if (games.Count == 0)
        {
            return 0;
        }

        var teamLines = await _repository.ListTeamStatlinesForCompetitionAsync(competitionId, ct);
        if (teamLines.Count == 0)
        {
            return 0;
        }

        var rules = games[0].RuleSet;
        var organisationId = games[0].OrganisationId;
        var groupByTeam = games
            .SelectMany(g => new[] { (g.HomeCompetitionTeamId, g.GroupId), (g.AwayCompetitionTeamId, g.GroupId) })
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Item2).FirstOrDefault(x => x is not null));

        var rows = new List<CompetitionStanding>();

        // Tables are computed per group; a competition without pools is one table.
        foreach (var group in teamLines.GroupBy(l => groupByTeam.GetValueOrDefault(l.CompetitionTeamId)))
        {
            var results = group
                .Select(l => new StandingsResult(l.CompetitionTeamId, l.OpponentCompetitionTeamId, l.Points, l.PointsAgainst))
                .ToList();

            var built = StandingsCalculator.Build(results, rules);
            var ordered = StandingsCalculator.Order(built, results, rules);

            var position = 1;
            foreach (var line in ordered)
            {
                var row = CompetitionStanding.Create(
                    organisationId, competitionId, group.Key, line.TeamId, line.Played, line.Won,
                    line.Lost, line.Drawn, line.PointsFor, line.PointsAgainst, line.LeaguePoints);
                row.SetPosition(position++);
                rows.Add(row);
            }
        }

        _repository.AddStandings(rows);
        return rows.Count;
    }
}
