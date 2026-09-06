using Hoops.Modules.Statistics.Application.Abstractions;
using Hoops.Modules.Statistics.Contracts;
using Hoops.Modules.Statistics.Domain;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Statistics.Application;

/// <summary>Reads the persisted statistics tables.</summary>
public sealed class StatisticsQueryService : IStatisticsQueryService
{
    private readonly IStatisticsRepository _repository;

    /// <summary>Creates the service.</summary>
    public StatisticsQueryService(IStatisticsRepository repository) => _repository = repository;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<StandingsRowDto>>> GetStandingsAsync(
        CompetitionId competitionId, CancellationToken ct = default)
    {
        var rows = await _repository.ListStandingsAsync(competitionId, ct);
        return Result.Success<IReadOnlyList<StandingsRowDto>>(rows.Select(ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CompetitionPlayerAggregateDto>>> GetCompetitionPlayersAsync(
        CompetitionId competitionId, bool qualifiedOnly, CancellationToken ct = default)
    {
        var aggregates = await _repository.ListCompetitionAggregatesAsync(competitionId, ct);
        var filtered = qualifiedOnly ? aggregates.Where(a => a.IsQualified) : aggregates;
        // The secondary sort is load-bearing: without it, players level on points come back in
        // whatever order the database happened to return, and a recompute would reshuffle them.
        return Result.Success<IReadOnlyList<CompetitionPlayerAggregateDto>>(
            filtered.OrderByDescending(a => a.Points).ThenBy(a => a.PlayerId.Value).Select(ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<PlayerCareerAggregateDto>> GetCareerAsync(PlayerId playerId, CancellationToken ct = default)
    {
        var career = await _repository.GetCareerAggregateAsync(playerId, ct);
        return career is null
            ? Error.NotFound("CAREER_NOT_FOUND", "This player has no finalised games yet.")
            : ToDto(career);
    }

    private static StandingsRowDto ToDto(CompetitionStanding s)
        => new(s.Position, s.CompetitionTeamId, s.GroupId, s.Played, s.Won, s.Lost, s.Drawn,
            s.PointsFor, s.PointsAgainst, s.PointDifferential, s.LeaguePoints);

    private static CompetitionPlayerAggregateDto ToDto(CompetitionPlayerAggregate a)
        => new(a.PlayerId, a.GamesPlayed, a.Points, a.PointsPerGame, a.FieldGoalsMade, a.FieldGoalsAttempted,
            a.ThreePointersMade, a.ThreePointersAttempted, a.FreeThrowsMade, a.FreeThrowsAttempted,
            a.TotalRebounds, a.Assists, a.Steals, a.Blocks, a.Turnovers, a.SecondsPlayed, a.PlusMinus, a.IsQualified);

    private static PlayerCareerAggregateDto ToDto(PlayerCareerAggregate c)
        => new(c.PlayerId, c.GamesPlayed, c.CompetitionsPlayed, c.Points, c.FieldGoalsMade, c.FieldGoalsAttempted,
            c.ThreePointersMade, c.ThreePointersAttempted, c.FreeThrowsMade, c.FreeThrowsAttempted,
            c.TotalRebounds, c.Assists, c.Steals, c.Blocks, c.Turnovers, c.SecondsPlayed);
}
