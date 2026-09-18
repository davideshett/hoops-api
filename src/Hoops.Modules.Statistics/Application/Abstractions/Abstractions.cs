using Hoops.Modules.Statistics.Domain;
using Hoops.SharedKernel;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Statistics.Application.Abstractions;

/// <summary>The facts about a finalised game that the statistics module needs, without reaching into
/// another module's tables. Supplied by Infrastructure.</summary>
/// <param name="GameId">The game.</param>
/// <param name="OrganisationId">The owning organisation.</param>
/// <param name="CompetitionId">The competition.</param>
/// <param name="HomeCompetitionTeamId">Home team.</param>
/// <param name="AwayCompetitionTeamId">Away team.</param>
/// <param name="GroupId">The group, when the competition has pools.</param>
/// <param name="RuleSet">The rule set snapshotted at roster lock.</param>
/// <param name="IsFinalized">Whether the game currently counts toward statistics.</param>
public sealed record GameFacts(
    GameId GameId,
    OrganisationId OrganisationId,
    CompetitionId CompetitionId,
    CompetitionTeamId HomeCompetitionTeamId,
    CompetitionTeamId AwayCompetitionTeamId,
    GroupId? GroupId,
    RuleSet RuleSet,
    bool IsFinalized);

/// <summary>
/// Reads game state and replays the event log through the projector. Implemented in Infrastructure so
/// the statistics module never references the GameRecording module's persistence.
/// </summary>
public interface IGameStatisticsSource
{
    /// <summary>The facts about one game, or null if it does not exist.</summary>
    Task<GameFacts?> GetFactsAsync(GameId gameId, CancellationToken ct = default);

    /// <summary>Every finalised game in a competition.</summary>
    Task<IReadOnlyList<GameFacts>> ListFinalizedForCompetitionAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>
    /// The organisation owning a competition, or null if it does not exist. Answered WITHOUT the
    /// tenant filter, because the recompute path runs as a system operation with no ambient tenant —
    /// it exists so an org-scoped caller can be checked against the real owner (see
    /// <see cref="Contracts.IStatisticsRecomputeService"/>), and works for a competition with no
    /// finalised games yet.
    /// </summary>
    Task<OrganisationId?> GetCompetitionOrganisationAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>How many games are scheduled in a competition (the qualification denominator).</summary>
    Task<int> CountScheduledGamesAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>Replays a game's event log and returns the derived statistics.</summary>
    Task<ProjectedGame?> ProjectAsync(GameId gameId, CancellationToken ct = default);
}

/// <summary>A game's projected statistics, flattened for persistence.</summary>
/// <param name="Players">Per-player lines.</param>
/// <param name="Teams">Per-team lines.</param>
/// <param name="Periods">Per-team, per-period lines.</param>
/// <param name="Stints">Lineup stints.</param>
public sealed record ProjectedGame(
    IReadOnlyList<ProjectedPlayerLine> Players,
    IReadOnlyList<ProjectedTeamLine> Teams,
    IReadOnlyList<ProjectedPeriodLine> Periods,
    IReadOnlyList<ProjectedStint> Stints);

/// <summary>A projected player line.</summary>
public sealed record ProjectedPlayerLine(
    GameRosterEntryId GameRosterEntryId, PlayerId PlayerId, CompetitionTeamId CompetitionTeamId,
    int Points, int Fgm, int Fga, int Tpm, int Tpa, int Ftm, int Fta, int Oreb, int Dreb,
    int Assists, int Steals, int Blocks, int BlocksAgainst, int Turnovers, int FoulsCommitted,
    int FoulsDrawn, bool FouledOut, int SecondsPlayed, int PlusMinus);

/// <summary>A projected team line.</summary>
public sealed record ProjectedTeamLine(
    CompetitionTeamId CompetitionTeamId, int Points, int Fgm, int Fga, int Tpm, int Tpa,
    int Ftm, int Fta, int Oreb, int Dreb, int Assists, int Steals, int Blocks, int Turnovers, int FoulsCommitted);

/// <summary>A projected per-team, per-period line.</summary>
public sealed record ProjectedPeriodLine(CompetitionTeamId CompetitionTeamId, int Period, int Points, int TeamFouls);

/// <summary>A projected lineup stint.</summary>
public sealed record ProjectedStint(
    CompetitionTeamId CompetitionTeamId, int Period, string PlayerIds, int SecondsPlayed, int PointsFor, int PointsAgainst);

/// <summary>Persistence for the derived statistics tables. All of these are fully rebuildable.</summary>
public interface IStatisticsRepository
{
    /// <summary>Deletes every persisted statistic for one game (statlines, periods, stints).</summary>
    Task DeleteGameStatisticsAsync(GameId gameId, CancellationToken ct = default);

    /// <summary>
    /// Deletes persisted statistics for EVERY game in a competition. A full recompute clears the slate
    /// this way rather than per finalised game, so a game that has LEFT the finalised set (reopened,
    /// forfeited, cancelled) cannot leave stale statlines contributing to leaderboards.
    /// </summary>
    Task DeleteCompetitionGameStatisticsAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>Stages persisted statistics for a game.</summary>
    void AddGameStatistics(
        IEnumerable<PlayerGameStatline> players, IEnumerable<TeamGameStatline> teams,
        IEnumerable<GamePeriodStateRow> periods, IEnumerable<LineupStintRow> stints);

    /// <summary>Every player statline in a competition.</summary>
    Task<IReadOnlyList<PlayerGameStatline>> ListPlayerStatlinesForCompetitionAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>Every team statline in a competition.</summary>
    Task<IReadOnlyList<TeamGameStatline>> ListTeamStatlinesForCompetitionAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>Every statline belonging to a set of players, across all competitions (for careers).</summary>
    Task<IReadOnlyList<PlayerGameStatline>> ListPlayerStatlinesForPlayersAsync(IReadOnlyCollection<PlayerId> playerIds, CancellationToken ct = default);

    /// <summary>Deletes a competition's player aggregates.</summary>
    Task DeleteCompetitionAggregatesAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>Stages competition player aggregates.</summary>
    void AddCompetitionAggregates(IEnumerable<CompetitionPlayerAggregate> aggregates);

    /// <summary>Deletes career aggregates for a set of players.</summary>
    Task DeleteCareerAggregatesAsync(IReadOnlyCollection<PlayerId> playerIds, CancellationToken ct = default);

    /// <summary>Stages career aggregates.</summary>
    void AddCareerAggregates(IEnumerable<PlayerCareerAggregate> aggregates);

    /// <summary>Deletes a competition's standings.</summary>
    Task DeleteStandingsAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>Stages standings rows.</summary>
    void AddStandings(IEnumerable<CompetitionStanding> standings);

    /// <summary>A competition's standings, in table order.</summary>
    Task<IReadOnlyList<CompetitionStanding>> ListStandingsAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>A competition's player aggregates.</summary>
    Task<IReadOnlyList<CompetitionPlayerAggregate>> ListCompetitionAggregatesAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>One player's career aggregate, or null.</summary>
    Task<PlayerCareerAggregate?> GetCareerAggregateAsync(PlayerId playerId, CancellationToken ct = default);

    /// <summary>A game's persisted player statlines.</summary>
    Task<IReadOnlyList<PlayerGameStatline>> ListPlayerStatlinesForGameAsync(GameId gameId, CancellationToken ct = default);

    /// <summary>A game's persisted team statlines.</summary>
    Task<IReadOnlyList<TeamGameStatline>> ListTeamStatlinesForGameAsync(GameId gameId, CancellationToken ct = default);
}

/// <summary>Persistence for the transactional outbox.</summary>
public interface IOutboxRepository
{
    /// <summary>Stages a message, to be committed with the state change that produced it.</summary>
    void Add(OutboxMessage message);

    /// <summary>The oldest pending messages, up to <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<OutboxMessage>> ListPendingAsync(int limit, CancellationToken ct = default);
}

/// <summary>Commits staged changes for the statistics module.</summary>
public interface IStatisticsUnitOfWork
{
    /// <summary>Persists all staged changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>One shot as stored in the event log, flattened for charting.</summary>
public sealed record ShotRow(
    GameId GameId, PlayerId? PlayerId, CompetitionTeamId? CompetitionTeamId, bool Made, int Points,
    int XCm, int YCm, string Zone, int DistanceCm, int Period, int GameClockMs);

/// <summary>One play-by-play row, resolved to registry players.</summary>
public sealed record PlayRow(
    long Sequence, int Period, int GameClockMs, string EventType, string? EventSubtype,
    CompetitionTeamId? CompetitionTeamId, PlayerId? PlayerId, PlayerId? SecondaryPlayerId,
    int? Points, string? ShotZone, int? ShotDistanceCm, bool IsVoided);

/// <summary>
/// Read models for the historical query surface. These read the derived tables and — for shot charts
/// and play-by-play, which need per-event detail no aggregate carries — the event log itself.
/// </summary>
public interface IHistoryReadRepository
{
    /// <summary>A game's play-by-play rows, in sequence order, resolved to registry players.</summary>
    Task<IReadOnlyList<PlayRow>> ListPlaysAsync(GameId gameId, int? period, CancellationToken ct = default);

    /// <summary>A game's shots, optionally filtered by team or player.</summary>
    Task<IReadOnlyList<ShotRow>> ListGameShotsAsync(
        GameId gameId, CompetitionTeamId? teamId, PlayerId? playerId, CancellationToken ct = default);

    /// <summary>A player's shots across one competition, or their whole career when null.</summary>
    Task<IReadOnlyList<ShotRow>> ListPlayerShotsAsync(
        PlayerId playerId, CompetitionId? competitionId, CancellationToken ct = default);

    /// <summary>A game's lineup stints, with each stint's players resolved to registry ids.</summary>
    Task<IReadOnlyList<(CompetitionTeamId TeamId, IReadOnlyList<PlayerId> Players, int Seconds, int For, int Against)>>
        ListLineupsAsync(GameId gameId, CancellationToken ct = default);

    /// <summary>Every statline in an organisation, for all-time records.</summary>
    Task<IReadOnlyList<PlayerGameStatline>> ListStatlinesForOrganisationAsync(
        OrganisationId organisationId, CancellationToken ct = default);

    /// <summary>Every competition aggregate a player has, for the career breakdown.</summary>
    Task<IReadOnlyList<CompetitionPlayerAggregate>> ListAggregatesForPlayerAsync(
        PlayerId playerId, CancellationToken ct = default);
}
