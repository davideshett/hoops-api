using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.GameRecording.Contracts;

/// <summary>Fixtures: creation, scheduling, generation, officials, and roster lock (§6 through RosterLocked).</summary>
public interface IGameService
{
    /// <summary>Lists a competition's games, optionally filtered by stage and status.</summary>
    Task<Result<IReadOnlyList<GameDto>>> ListAsync(CompetitionId competitionId, StageId? stageId, string? status, CancellationToken ct = default);

    /// <summary>Creates a single fixture.</summary>
    Task<Result<GameDto>> CreateAsync(OrganisationId organisationId, CompetitionId competitionId, CreateGameRequest request, CancellationToken ct = default);

    /// <summary>Fetches a game.</summary>
    Task<Result<GameDto>> GetAsync(GameId id, CancellationToken ct = default);

    /// <summary>Reschedules a fixture (time/venue). Allowed only before roster lock.</summary>
    Task<Result<GameDto>> RescheduleAsync(GameId id, RescheduleGameRequest request, CancellationToken ct = default);

    /// <summary>Deletes a fixture. Allowed only while Scheduled.</summary>
    Task<Result> DeleteAsync(GameId id, CancellationToken ct = default);

    /// <summary>Postpones a scheduled game.</summary>
    Task<Result<GameDto>> PostponeAsync(GameId id, CancellationToken ct = default);

    /// <summary>Cancels a scheduled or postponed game.</summary>
    Task<Result<GameDto>> CancelAsync(GameId id, CancellationToken ct = default);

    /// <summary>Generates a round-robin schedule from the competition's entered teams.</summary>
    Task<Result<IReadOnlyList<GameDto>>> GenerateRoundRobinAsync(OrganisationId organisationId, CompetitionId competitionId, GenerateRoundRobinRequest request, CancellationToken ct = default);

    /// <summary>Generates a seeded knockout bracket's first round from the competition's entered teams.</summary>
    Task<Result<IReadOnlyList<GameDto>>> GenerateKnockoutAsync(OrganisationId organisationId, CompetitionId competitionId, GenerateKnockoutRequest request, CancellationToken ct = default);

    /// <summary>Lists a game's officials.</summary>
    Task<Result<IReadOnlyList<GameOfficialDto>>> ListOfficialsAsync(GameId id, CancellationToken ct = default);

    /// <summary>Assigns an official to a game.</summary>
    Task<Result<GameOfficialDto>> AssignOfficialAsync(OrganisationId organisationId, GameId id, AssignOfficialRequest request, CancellationToken ct = default);

    /// <summary>The setup screen: fixture, rule set, and both teams' current rosters.</summary>
    Task<Result<GameSetupDto>> GetSetupAsync(GameId id, CancellationToken ct = default);

    /// <summary>
    /// Locks the roster: validates minimum size and starter count from the rule set, freezes the rule
    /// set and a roster snapshot onto the game, and transitions to RosterLocked.
    /// </summary>
    Task<Result<GameDto>> LockRosterAsync(GameId id, LockRosterRequest request, CancellationToken ct = default);

    /// <summary>The frozen game-roster snapshot.</summary>
    Task<Result<IReadOnlyList<GameRosterEntryDto>>> GetGameRosterAsync(GameId id, CancellationToken ct = default);
}
