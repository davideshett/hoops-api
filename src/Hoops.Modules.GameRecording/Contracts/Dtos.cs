using Hoops.SharedKernel;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.GameRecording.Contracts;

/// <summary>A fixture.</summary>
public sealed record GameDto(
    GameId Id,
    CompetitionId CompetitionId,
    StageId? StageId,
    GroupId? GroupId,
    CompetitionTeamId HomeCompetitionTeamId,
    CompetitionTeamId AwayCompetitionTeamId,
    VenueId? VenueId,
    DateTimeOffset ScheduledAt,
    string Status,
    RuleSet? RuleSetSnapshot,
    DateTimeOffset? RosterLockedAt);

/// <summary>Create a single fixture.</summary>
public sealed record CreateGameRequest(
    CompetitionTeamId HomeCompetitionTeamId,
    CompetitionTeamId AwayCompetitionTeamId,
    DateTimeOffset ScheduledAt,
    StageId? StageId,
    GroupId? GroupId,
    VenueId? VenueId);

/// <summary>Reschedule a fixture (before roster lock). Set <paramref name="ClearVenue"/> to remove the venue.</summary>
public sealed record RescheduleGameRequest(DateTimeOffset? ScheduledAt, VenueId? VenueId, bool ClearVenue);

/// <summary>Generate a round-robin schedule from a competition's entered teams.</summary>
public sealed record GenerateRoundRobinRequest(bool DoubleRound, StageId? StageId, DateTimeOffset FirstGameAt);

/// <summary>Generate a seeded knockout bracket's first round from a competition's entered teams.</summary>
public sealed record GenerateKnockoutRequest(StageId? StageId, DateTimeOffset FirstGameAt);

/// <summary>Assign an official to a game.</summary>
public sealed record AssignOfficialRequest(string FullName, string Role);

/// <summary>A game official.</summary>
public sealed record GameOfficialDto(GameOfficialId Id, GameId GameId, string FullName, string Role);

/// <summary>One selected roster entry at roster lock, with whether the player starts.</summary>
public sealed record GameRosterSelection(RosterEntryId RosterEntryId, bool IsStarter);

/// <summary>Lock the roster: the set of players (by roster-entry id) available for this game, and who starts.</summary>
public sealed record LockRosterRequest(IReadOnlyList<GameRosterSelection> Selections);

/// <summary>A frozen game-roster snapshot row, with the player's display name for the scorer's table.</summary>
public sealed record GameRosterEntryDto(
    GameRosterEntryId Id, CompetitionTeamId CompetitionTeamId, PlayerId PlayerId, string FullName,
    string JerseyNumber, string? Position, bool IsStarter, bool IsCaptain);

/// <summary>One player currently on a team's roster, offered on the game-setup screen.</summary>
public sealed record AvailablePlayerDto(
    RosterEntryId RosterEntryId, PlayerId PlayerId, string FullName, string JerseyNumber, string? Position,
    bool IsCaptain, int VerifiedTier);

/// <summary>A team's current roster for a game's setup, with the labels the app shows.</summary>
public sealed record TeamRosterDto(
    CompetitionTeamId CompetitionTeamId, string Name, string ShortName, string? Abbreviation,
    IReadOnlyList<AvailablePlayerDto> Players);

/// <summary>The game-setup screen: the fixture, its rule set, and both teams' current rosters.</summary>
public sealed record GameSetupDto(GameDto Game, RuleSet RuleSet, IReadOnlyList<TeamRosterDto> Teams);

/// <summary>Reopen a finalised game. A reason is mandatory and audited.</summary>
public sealed record ReopenGameRequest(string Reason);

/// <summary>Forfeit a game in favour of one team. A reason is mandatory and audited.</summary>
public sealed record ForfeitGameRequest(CompetitionTeamId WinningCompetitionTeamId, string Reason);
