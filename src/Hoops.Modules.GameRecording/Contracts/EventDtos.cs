using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.GameRecording.Contracts;

/// <summary>
/// Submission of one event (§11). <paramref name="EventId"/> is CLIENT-generated and is the
/// idempotency key: replaying it is safe by construction (ADR-007).
/// </summary>
public sealed record SubmitEventRequest(
    Guid EventId,
    long? LastKnownSequence,
    string EventType,
    string? EventSubtype,
    int Period,
    int GameClockMs,
    int? ShotClockMs,
    CompetitionTeamId? CompetitionTeamId,
    GameRosterEntryId? GameRosterEntryId,
    GameRosterEntryId? SecondaryRosterEntryId,
    int? Points,
    int? ShotXCm,
    int? ShotYCm,
    DateTimeOffset? ClientRecordedAt,
    IReadOnlyDictionary<string, string>? Payload);

/// <summary>An ordered batch (free-throw sequences, substitution groups).</summary>
public sealed record SubmitEventBatchRequest(long? LastKnownSequence, IReadOnlyList<SubmitEventRequest> Events);

/// <summary>Void an event, with a reason.</summary>
public sealed record VoidEventRequest(string Reason);

/// <summary>A recorded event as returned to clients.</summary>
public sealed record GameEventDto(
    Guid EventId,
    long Sequence,
    string EventType,
    string? EventSubtype,
    int Period,
    int GameClockMs,
    CompetitionTeamId? CompetitionTeamId,
    GameRosterEntryId? GameRosterEntryId,
    GameRosterEntryId? SecondaryRosterEntryId,
    int? Points,
    int? ShotXCm,
    int? ShotYCm,
    string? ShotZone,
    int? ShotDistanceCm,
    bool IsVoided,
    bool WasOverridden,
    IReadOnlyDictionary<string, string> Payload);

/// <summary>The live state the recording app renders, derived from the log.</summary>
public sealed record LiveGameStateDto(
    GameId GameId,
    long LastSequence,
    int CurrentPeriod,
    int GameClockMs,
    bool ClockRunning,
    bool GameStarted,
    bool GameEnded,
    bool PeriodEnded,
    IReadOnlyDictionary<string, int> Score,
    IReadOnlyDictionary<string, int> TeamFouls,
    IReadOnlyDictionary<string, int> TimeoutsRemaining,
    IReadOnlyDictionary<string, IReadOnlyList<GameRosterEntryId>> OnCourt,
    IReadOnlyList<GameRosterEntryId> FouledOut);

/// <summary>The result of accepting an event: its sequence plus the full state, so the client never guesses.</summary>
public sealed record EventAcceptedDto(long Sequence, GameEventDto Event, LiveGameStateDto State);

/// <summary>A player's derived statline.</summary>
public sealed record PlayerStatlineDto(
    GameRosterEntryId GameRosterEntryId, PlayerId PlayerId, CompetitionTeamId CompetitionTeamId,
    int Points, int FieldGoalsMade, int FieldGoalsAttempted, int ThreePointersMade, int ThreePointersAttempted,
    int FreeThrowsMade, int FreeThrowsAttempted, int OffensiveRebounds, int DefensiveRebounds, int TotalRebounds,
    int Assists, int Steals, int Blocks, int Turnovers, int FoulsCommitted, int FoulsDrawn, bool FouledOut,
    int SecondsPlayed, int PlusMinus,
    double? FieldGoalPercentage, double? ThreePointPercentage, double? FreeThrowPercentage, int Efficiency);

/// <summary>A team's derived statline.</summary>
public sealed record TeamStatlineDto(
    CompetitionTeamId CompetitionTeamId, int Points, int FieldGoalsMade, int FieldGoalsAttempted,
    int ThreePointersMade, int ThreePointersAttempted, int FreeThrowsMade, int FreeThrowsAttempted,
    int OffensiveRebounds, int DefensiveRebounds, int TotalRebounds, int Assists, int Steals, int Blocks,
    int Turnovers, int FoulsCommitted);

/// <summary>The box score: both teams' statlines and every player's.</summary>
public sealed record BoxScoreDto(
    GameId GameId, IReadOnlyList<TeamStatlineDto> Teams, IReadOnlyList<PlayerStatlineDto> Players);

/// <summary>A short-lived, game-scoped token for a volunteer official at the scorer's table.</summary>
public sealed record GameAccessTokenDto(string AccessToken, DateTimeOffset ExpiresAt, GameId GameId);
