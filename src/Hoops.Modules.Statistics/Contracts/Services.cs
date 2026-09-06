using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Statistics.Contracts;

/// <summary>
/// Rebuilds derived statistics from the event log. Safe to run at any time — it is simultaneously the
/// disaster-recovery path, the correctness audit, and the thing that makes ADR-001 real (§9.3).
/// </summary>
public interface IStatisticsRecomputeService
{
    /// <summary>Rebuilds one game's statistics, then its competition's aggregates and standings.</summary>
    Task<Result<RecomputeSummaryDto>> RecomputeGameAsync(GameId gameId, CancellationToken ct = default);

    /// <summary>Rebuilds an entire competition: every game, its aggregates, standings, and careers.</summary>
    Task<Result<RecomputeSummaryDto>> RecomputeCompetitionAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>Rebuilds career totals for the given players.</summary>
    Task<Result<RecomputeSummaryDto>> RecomputeCareersAsync(IReadOnlyCollection<PlayerId> playerIds, CancellationToken ct = default);
}

/// <summary>Read access to the persisted statistics.</summary>
public interface IStatisticsQueryService
{
    /// <summary>A competition's standings, in table order.</summary>
    Task<Result<IReadOnlyList<StandingsRowDto>>> GetStandingsAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>A competition's player aggregates, optionally only qualified players.</summary>
    Task<Result<IReadOnlyList<CompetitionPlayerAggregateDto>>> GetCompetitionPlayersAsync(
        CompetitionId competitionId, bool qualifiedOnly, CancellationToken ct = default);

    /// <summary>A player's career totals.</summary>
    Task<Result<PlayerCareerAggregateDto>> GetCareerAsync(PlayerId playerId, CancellationToken ct = default);
}
