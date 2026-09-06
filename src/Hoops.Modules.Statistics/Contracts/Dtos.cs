using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Statistics.Contracts;

/// <summary>What a recompute rebuilt.</summary>
/// <param name="GamesProcessed">Games replayed.</param>
/// <param name="StatlineRowsWritten">Statline, period, and stint rows written.</param>
/// <param name="AggregatesWritten">Aggregate rows written.</param>
/// <param name="StandingsRowsWritten">Standings rows written.</param>
public sealed record RecomputeSummaryDto(
    int GamesProcessed, int StatlineRowsWritten, int AggregatesWritten, int StandingsRowsWritten);

/// <summary>A team's standings row.</summary>
public sealed record StandingsRowDto(
    int Position, CompetitionTeamId CompetitionTeamId, GroupId? GroupId, int Played, int Won, int Lost,
    int Drawn, int PointsFor, int PointsAgainst, int PointDifferential, int LeaguePoints);

/// <summary>A player's totals in one competition.</summary>
public sealed record CompetitionPlayerAggregateDto(
    PlayerId PlayerId, int GamesPlayed, int Points, double? PointsPerGame, int FieldGoalsMade,
    int FieldGoalsAttempted, int ThreePointersMade, int ThreePointersAttempted, int FreeThrowsMade,
    int FreeThrowsAttempted, int TotalRebounds, int Assists, int Steals, int Blocks, int Turnovers,
    int SecondsPlayed, int PlusMinus, bool IsQualified);

/// <summary>A player's career totals across every organisation.</summary>
public sealed record PlayerCareerAggregateDto(
    PlayerId PlayerId, int GamesPlayed, int CompetitionsPlayed, int Points, int FieldGoalsMade,
    int FieldGoalsAttempted, int ThreePointersMade, int ThreePointersAttempted, int FreeThrowsMade,
    int FreeThrowsAttempted, int TotalRebounds, int Assists, int Steals, int Blocks, int Turnovers,
    int SecondsPlayed);
