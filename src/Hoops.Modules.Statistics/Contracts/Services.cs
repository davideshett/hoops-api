using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Statistics.Contracts;

/// <summary>
/// Rebuilds derived statistics from the event log. Safe to run at any time — it is simultaneously the
/// disaster-recovery path, the correctness audit, and the thing that makes ADR-001 real (§9.3).
/// </summary>
/// <remarks>
/// Recompute reads and writes across the tenant filter by necessity — it replays a game the ambient
/// tenant may not own (the outbox drainer has no tenant at all). <c>requiredOrganisationId</c> is
/// therefore how an org-scoped caller proves it is entitled to the target: pass the route's
/// organisation and a game or competition belonging to anyone else reports NOT FOUND, exactly as the
/// query filter would. System callers pass <c>null</c>. There is no default — every caller states
/// which it is.
/// </remarks>
public interface IStatisticsRecomputeService
{
    /// <summary>Rebuilds one game's statistics, then its competition's aggregates and standings.</summary>
    Task<Result<RecomputeSummaryDto>> RecomputeGameAsync(
        GameId gameId, OrganisationId? requiredOrganisationId, CancellationToken ct = default);

    /// <summary>Rebuilds an entire competition: every game, its aggregates, standings, and careers.</summary>
    Task<Result<RecomputeSummaryDto>> RecomputeCompetitionAsync(
        CompetitionId competitionId, OrganisationId? requiredOrganisationId, CancellationToken ct = default);

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

/// <summary>
/// The historical query surface — the thing the product is actually sold on (§11). Every figure here
/// is read from the derived tables, which are themselves rebuildable from the event log.
/// </summary>
public interface IHistoryQueryService
{
    /// <summary>One leaderboard. <paramref name="per"/> is <c>total</c> or <c>game</c>; per-game boards
    /// include only qualified players, totals boards include everyone (§9.3).</summary>
    Task<Result<LeaderboardDto>> GetLeadersAsync(
        CompetitionId competitionId, string stat, string per, int limit, CancellationToken ct = default);

    /// <summary>Every headline leaderboard in one round trip.</summary>
    Task<Result<AllLeadersDto>> GetAllLeadersAsync(
        CompetitionId competitionId, string per, int limit, CancellationToken ct = default);

    /// <summary>A player's career totals plus the per-competition breakdown.</summary>
    Task<Result<CareerPageDto>> GetCareerPageAsync(PlayerId playerId, CancellationToken ct = default);

    /// <summary>A game's play-by-play, with the running score after each entry.</summary>
    Task<Result<IReadOnlyList<PlayByPlayEntryDto>>> GetPlayByPlayAsync(
        GameId gameId, int? period, CancellationToken ct = default);

    /// <summary>A game's shot chart, optionally filtered to one team or player.</summary>
    Task<Result<ShotChartDto>> GetGameShotChartAsync(
        GameId gameId, CompetitionTeamId? teamId, PlayerId? playerId, CancellationToken ct = default);

    /// <summary>A player's shot chart across one competition, or their whole career when null.</summary>
    Task<Result<ShotChartDto>> GetPlayerShotChartAsync(
        PlayerId playerId, CompetitionId? competitionId, CancellationToken ct = default);

    /// <summary>A game's lineups, aggregated from its stints.</summary>
    Task<Result<IReadOnlyList<LineupSummaryDto>>> GetLineupsAsync(GameId gameId, CancellationToken ct = default);

    /// <summary>An organisation's all-time single-game and career records.</summary>
    Task<Result<OrganisationRecordsDto>> GetRecordsAsync(
        OrganisationId organisationId, int limit, CancellationToken ct = default);
}
