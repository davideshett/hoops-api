using Hoops.Modules.Statistics.Application.Abstractions;
using Hoops.Modules.Statistics.Contracts;
using Hoops.Modules.Statistics.Domain;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Statistics.Application;

/// <summary>The historical query surface (§11) — leaderboards, careers, shot charts, and records.</summary>
public sealed class HistoryQueryService : IHistoryQueryService
{
    /// <summary>Ranked by <c>total</c>: every player counts.</summary>
    public const string PerTotal = "total";

    /// <summary>Ranked by <c>game</c>: only qualified players count (§9.3).</summary>
    public const string PerGame = "game";

    /// <summary>The headline stats shown on a competition's home screen.</summary>
    public static readonly IReadOnlyList<string> HeadlineStats =
        ["points", "rebounds", "assists", "steals", "blocks", "threePointersMade", "efficiency"];

    private readonly IStatisticsRepository _statistics;
    private readonly IHistoryReadRepository _history;

    /// <summary>Creates the service.</summary>
    public HistoryQueryService(IStatisticsRepository statistics, IHistoryReadRepository history)
    {
        _statistics = statistics;
        _history = history;
    }

    /// <inheritdoc />
    public async Task<Result<LeaderboardDto>> GetLeadersAsync(
        CompetitionId competitionId, string stat, string per, int limit, CancellationToken ct = default)
    {
        if (!TryNormalise(stat, out var normalisedStat))
        {
            return Error.Validation("INVALID_STAT", $"'{stat}' is not a leaderboard stat.");
        }

        if (!TryPer(per, out var normalisedPer))
        {
            return Error.Validation("INVALID_PER", "Rank by 'total' or by 'game'.");
        }

        var aggregates = await _statistics.ListCompetitionAggregatesAsync(competitionId, ct);
        return Rank(aggregates, normalisedStat, normalisedPer, limit);
    }

    /// <inheritdoc />
    public async Task<Result<AllLeadersDto>> GetAllLeadersAsync(
        CompetitionId competitionId, string per, int limit, CancellationToken ct = default)
    {
        if (!TryPer(per, out var normalisedPer))
        {
            return Error.Validation("INVALID_PER", "Rank by 'total' or by 'game'.");
        }

        // ONE read, then every board ranked in memory. This endpoint exists precisely so the home
        // screen costs a single round trip rather than one per stat (§11).
        var aggregates = await _statistics.ListCompetitionAggregatesAsync(competitionId, ct);
        var boards = HeadlineStats.Select(stat => Rank(aggregates, stat, normalisedPer, limit)).ToList();
        return new AllLeadersDto(competitionId, normalisedPer, boards);
    }

    /// <inheritdoc />
    public async Task<Result<CareerPageDto>> GetCareerPageAsync(PlayerId playerId, CancellationToken ct = default)
    {
        var career = await _statistics.GetCareerAggregateAsync(playerId, ct);
        if (career is null)
        {
            return Error.NotFound("CAREER_NOT_FOUND", "This player has no finalised games yet.");
        }

        var byCompetition = (await _history.ListAggregatesForPlayerAsync(playerId, ct))
            .OrderByDescending(a => a.Points)
            .Select(a => new CareerCompetitionLineDto(
                a.CompetitionId, a.GamesPlayed, a.Points, a.PointsPerGame, a.TotalRebounds,
                a.Assists, a.Steals, a.Blocks, a.SecondsPlayed))
            .ToList();

        var totals = new PlayerCareerAggregateDto(
            career.PlayerId, career.GamesPlayed, career.CompetitionsPlayed, career.Points,
            career.FieldGoalsMade, career.FieldGoalsAttempted, career.ThreePointersMade,
            career.ThreePointersAttempted, career.FreeThrowsMade, career.FreeThrowsAttempted,
            career.TotalRebounds, career.Assists, career.Steals, career.Blocks, career.Turnovers,
            career.SecondsPlayed);

        return new CareerPageDto(playerId, totals, byCompetition);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PlayByPlayEntryDto>>> GetPlayByPlayAsync(
        GameId gameId, int? period, CancellationToken ct = default)
    {
        var plays = await _history.ListPlaysAsync(gameId, period, ct);

        // The running score is carried forward as the feed is built, so each entry shows the score as
        // it stood at that moment — which is what a play-by-play is for.
        var score = new Dictionary<CompetitionTeamId, int>();
        var entries = new List<PlayByPlayEntryDto>(plays.Count);

        foreach (var play in plays)
        {
            if (!play.IsVoided && play.Points is > 0 && play.CompetitionTeamId is { } team)
            {
                score[team] = score.GetValueOrDefault(team) + play.Points.Value;
            }

            entries.Add(new PlayByPlayEntryDto(
                play.Sequence, play.Period, play.GameClockMs, play.EventType, play.EventSubtype,
                play.CompetitionTeamId, play.PlayerId, play.SecondaryPlayerId, play.Points,
                play.ShotZone, play.ShotDistanceCm, play.IsVoided,
                new Dictionary<CompetitionTeamId, int>(score)));
        }

        return Result.Success<IReadOnlyList<PlayByPlayEntryDto>>(entries);
    }

    /// <inheritdoc />
    public async Task<Result<ShotChartDto>> GetGameShotChartAsync(
        GameId gameId, CompetitionTeamId? teamId, PlayerId? playerId, CancellationToken ct = default)
        => ToChart(await _history.ListGameShotsAsync(gameId, teamId, playerId, ct));

    /// <inheritdoc />
    public async Task<Result<ShotChartDto>> GetPlayerShotChartAsync(
        PlayerId playerId, CompetitionId? competitionId, CancellationToken ct = default)
        => ToChart(await _history.ListPlayerShotsAsync(playerId, competitionId, ct));

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<LineupSummaryDto>>> GetLineupsAsync(
        GameId gameId, CancellationToken ct = default)
    {
        var stints = await _history.ListLineupsAsync(gameId, ct);

        // Stints are per-span; a lineup's game total is the sum of every span it was on court for.
        var summaries = stints
            .GroupBy(s => (s.TeamId, Key: string.Join(',', s.Players.Select(p => p.Value).OrderBy(v => v))))
            .Select(g => new LineupSummaryDto(
                g.Key.TeamId,
                g.First().Players,
                g.Sum(s => s.Seconds),
                g.Sum(s => s.For),
                g.Sum(s => s.Against),
                g.Sum(s => s.For) - g.Sum(s => s.Against)))
            .OrderByDescending(l => l.SecondsPlayed)
            .ToList();

        return Result.Success<IReadOnlyList<LineupSummaryDto>>(summaries);
    }

    /// <inheritdoc />
    public async Task<Result<OrganisationRecordsDto>> GetRecordsAsync(
        OrganisationId organisationId, int limit, CancellationToken ct = default)
    {
        var lines = await _history.ListStatlinesForOrganisationAsync(organisationId, ct);

        // Single-game records: the best individual performance in any one game.
        var singleGame = new List<RecordHolderDto>();
        foreach (var (stat, selector) in StatlineSelectors)
        {
            singleGame.AddRange(lines
                .OrderByDescending(selector).ThenBy(l => l.PlayerId.Value)
                .Take(limit)
                .Select(l => new RecordHolderDto(stat, l.PlayerId, selector(l), l.GameId)));
        }

        // Career records within this organisation: totals across every game played here.
        var career = new List<RecordHolderDto>();
        var byPlayer = lines.GroupBy(l => l.PlayerId).ToList();
        foreach (var (stat, selector) in StatlineSelectors)
        {
            career.AddRange(byPlayer
                .Select(g => (PlayerId: g.Key, Value: g.Sum(selector)))
                .OrderByDescending(x => x.Value).ThenBy(x => x.PlayerId.Value)
                .Take(limit)
                .Select(x => new RecordHolderDto(stat, x.PlayerId, x.Value, null)));
        }

        return new OrganisationRecordsDto(singleGame, career);
    }

    // ── ranking ──────────────────────────────────────────────────────────────

    private static LeaderboardDto Rank(
        IReadOnlyList<CompetitionPlayerAggregate> aggregates, string stat, string per, int limit)
    {
        // Per-game boards require qualification; totals boards include everyone. Without this, a player
        // with one 30-point game would top the scoring average for the rest of the season (§9.3).
        var eligible = per == PerGame ? aggregates.Where(a => a.IsQualified) : aggregates;
        var selector = AggregateSelectors[stat];

        var rows = eligible
            .Select(a => (Aggregate: a, Total: selector(a)))
            .OrderByDescending(x => per == PerGame && x.Aggregate.GamesPlayed > 0
                ? (double)x.Total / x.Aggregate.GamesPlayed
                : x.Total)
            // A stable secondary key so equal values never reshuffle between calls.
            .ThenBy(x => x.Aggregate.PlayerId.Value)
            .Take(limit)
            .Select((x, index) => new LeaderRowDto(
                index + 1, x.Aggregate.PlayerId, x.Aggregate.GamesPlayed, x.Total,
                x.Aggregate.GamesPlayed == 0 ? null : (double)x.Total / x.Aggregate.GamesPlayed))
            .ToList();

        return new LeaderboardDto(stat, per, rows);
    }

    private static ShotChartDto ToChart(IReadOnlyList<ShotRow> shots)
    {
        var byZone = shots
            .GroupBy(s => s.Zone)
            .Select(g =>
            {
                var attempted = g.Count();
                var made = g.Count(s => s.Made);
                return new ShotZoneSummaryDto(g.Key, made, attempted,
                    attempted == 0 ? null : (double)made / attempted);
            })
            .OrderBy(z => z.Zone)
            .ToList();

        return new ShotChartDto(
            shots.Select(s => new ShotDto(
                s.GameId, s.PlayerId, s.CompetitionTeamId, s.Made, s.Points, s.XCm, s.YCm,
                s.Zone, s.DistanceCm, s.Period, s.GameClockMs)).ToList(),
            byZone);
    }

    private static readonly Dictionary<string, Func<CompetitionPlayerAggregate, int>> AggregateSelectors =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["points"] = a => a.Points,
            ["rebounds"] = a => a.TotalRebounds,
            ["assists"] = a => a.Assists,
            ["steals"] = a => a.Steals,
            ["blocks"] = a => a.Blocks,
            ["threePointersMade"] = a => a.ThreePointersMade,
            ["fieldGoalsMade"] = a => a.FieldGoalsMade,
            ["freeThrowsMade"] = a => a.FreeThrowsMade,
            ["turnovers"] = a => a.Turnovers,
            ["minutes"] = a => a.SecondsPlayed,
            ["efficiency"] = a => a.Points + a.TotalRebounds + a.Assists + a.Steals + a.Blocks
                - (a.FieldGoalsAttempted - a.FieldGoalsMade)
                - (a.FreeThrowsAttempted - a.FreeThrowsMade)
                - a.Turnovers,
        };

    private static readonly (string Stat, Func<PlayerGameStatline, int> Selector)[] StatlineSelectors =
    [
        ("points", l => l.Points),
        ("rebounds", l => l.TotalRebounds),
        ("assists", l => l.Assists),
        ("steals", l => l.Steals),
        ("blocks", l => l.Blocks),
        ("threePointersMade", l => l.ThreePointersMade),
    ];

    private static bool TryNormalise(string stat, out string normalised)
    {
        var match = AggregateSelectors.Keys.FirstOrDefault(k => string.Equals(k, stat, StringComparison.OrdinalIgnoreCase));
        normalised = match ?? string.Empty;
        return match is not null;
    }

    private static bool TryPer(string per, out string normalised)
    {
        if (string.IsNullOrWhiteSpace(per) || string.Equals(per, PerTotal, StringComparison.OrdinalIgnoreCase))
        {
            normalised = PerTotal;
            return true;
        }

        if (string.Equals(per, PerGame, StringComparison.OrdinalIgnoreCase))
        {
            normalised = PerGame;
            return true;
        }

        normalised = string.Empty;
        return false;
    }
}
