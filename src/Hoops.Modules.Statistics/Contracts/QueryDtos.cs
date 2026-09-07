using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Statistics.Contracts;

// ── Leaderboards ─────────────────────────────────────────────────────────────

/// <summary>One row on a leaderboard.</summary>
/// <param name="Rank">1-based position.</param>
/// <param name="PlayerId">The player.</param>
/// <param name="GamesPlayed">Games played in the competition.</param>
/// <param name="Total">The stat's total across those games.</param>
/// <param name="PerGame">The per-game average, or null when no games played.</param>
public sealed record LeaderRowDto(int Rank, PlayerId PlayerId, int GamesPlayed, int Total, double? PerGame);

/// <summary>A single leaderboard.</summary>
/// <param name="Stat">The stat, as requested.</param>
/// <param name="Per">Whether rows are ranked by <c>total</c> or by <c>game</c>.</param>
/// <param name="Rows">The ranked rows.</param>
public sealed record LeaderboardDto(string Stat, string Per, IReadOnlyList<LeaderRowDto> Rows);

/// <summary>
/// Every headline leaderboard in one payload. This exists deliberately: it is the competition's home
/// screen, and it should not cost six round trips (§11).
/// </summary>
/// <param name="CompetitionId">The competition.</param>
/// <param name="Per">Whether the boards are totals or per-game.</param>
/// <param name="Leaderboards">One board per headline stat.</param>
public sealed record AllLeadersDto(
    CompetitionId CompetitionId, string Per, IReadOnlyList<LeaderboardDto> Leaderboards);

// ── Career ───────────────────────────────────────────────────────────────────

/// <summary>A player's totals within one competition, for the career breakdown.</summary>
public sealed record CareerCompetitionLineDto(
    CompetitionId CompetitionId, int GamesPlayed, int Points, double? PointsPerGame, int TotalRebounds,
    int Assists, int Steals, int Blocks, int SecondsPlayed);

/// <summary>
/// A player's career page: totals across every organisation, plus the per-competition breakdown that
/// makes "what has this player actually done" answerable.
/// </summary>
public sealed record CareerPageDto(
    PlayerId PlayerId, PlayerCareerAggregateDto Totals, IReadOnlyList<CareerCompetitionLineDto> ByCompetition);

// ── Shot charts ──────────────────────────────────────────────────────────────

/// <summary>Shooting from one court zone.</summary>
/// <param name="Zone">The zone, as classified by the server (§8).</param>
/// <param name="Made">Shots made.</param>
/// <param name="Attempted">Shots attempted.</param>
/// <param name="Percentage">Make rate, or null when there were no attempts.</param>
public sealed record ShotZoneSummaryDto(string Zone, int Made, int Attempted, double? Percentage);

/// <summary>One shot, for plotting.</summary>
public sealed record ShotDto(
    GameId GameId, PlayerId? PlayerId, CompetitionTeamId? CompetitionTeamId, bool Made, int Points,
    int XCm, int YCm, string Zone, int DistanceCm, int Period, int GameClockMs);

/// <summary>A shot chart: the individual shots plus their per-zone aggregation.</summary>
public sealed record ShotChartDto(IReadOnlyList<ShotDto> Shots, IReadOnlyList<ShotZoneSummaryDto> ByZone);

// ── Play-by-play ─────────────────────────────────────────────────────────────

/// <summary>One entry in the play-by-play feed, in game order.</summary>
public sealed record PlayByPlayEntryDto(
    long Sequence, int Period, int GameClockMs, string EventType, string? EventSubtype,
    CompetitionTeamId? CompetitionTeamId, PlayerId? PlayerId, PlayerId? SecondaryPlayerId,
    int? Points, string? ShotZone, int? ShotDistanceCm, bool IsVoided,
    IReadOnlyDictionary<CompetitionTeamId, int> ScoreAfter);

// ── Lineups ──────────────────────────────────────────────────────────────────

/// <summary>A lineup's aggregated performance across a game.</summary>
public sealed record LineupSummaryDto(
    CompetitionTeamId CompetitionTeamId, IReadOnlyList<PlayerId> PlayerIds, int SecondsPlayed,
    int PointsFor, int PointsAgainst, int PlusMinus);

// ── Records ──────────────────────────────────────────────────────────────────

/// <summary>A single record holder.</summary>
/// <param name="Stat">Which record.</param>
/// <param name="PlayerId">The holder.</param>
/// <param name="Value">The record value.</param>
/// <param name="GameId">The game it was set in, for single-game records.</param>
public sealed record RecordHolderDto(string Stat, PlayerId PlayerId, int Value, GameId? GameId);

/// <summary>An organisation's all-time records, single-game and career.</summary>
public sealed record OrganisationRecordsDto(
    IReadOnlyList<RecordHolderDto> SingleGame, IReadOnlyList<RecordHolderDto> Career);
