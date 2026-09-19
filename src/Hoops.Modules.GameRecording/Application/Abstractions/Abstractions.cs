using Hoops.Modules.GameRecording.Domain;
using Hoops.SharedKernel;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.GameRecording.Application.Abstractions;

/// <summary>A roster entry as seen by the game module for the setup screen and lock snapshot.</summary>
/// <param name="RosterEntryId">The source roster entry.</param>
/// <param name="CompetitionTeamId">The team the entry belongs to.</param>
/// <param name="PlayerId">The registry player.</param>
/// <param name="JerseyNumber">Jersey number (current).</param>
/// <param name="Position">Position, if any.</param>
/// <param name="IsCaptain">Whether the player is captain.</param>
/// <param name="VerifiedTier">Identity tier snapshotted at registration.</param>
/// <param name="IsActive">Whether the entry is active (eligible to play).</param>
public sealed record RosterSnapshotRow(
    RosterEntryId RosterEntryId, CompetitionTeamId CompetitionTeamId, PlayerId PlayerId,
    string JerseyNumber, string? Position, bool IsCaptain, int VerifiedTier, bool IsActive);

/// <summary>An entered competition team, with its seed if set.</summary>
public sealed record EnteredTeam(CompetitionTeamId Id, int? Seed);

/// <summary>Persistence for <see cref="Game"/> (tenant-scoped).</summary>
public interface IGameRepository
{
    /// <summary>Finds a game by id, or null.</summary>
    Task<Game?> GetAsync(GameId id, CancellationToken ct = default);

    /// <summary>Lists a competition's games, optionally filtered by stage and status.</summary>
    Task<IReadOnlyList<Game>> ListForCompetitionAsync(CompetitionId competitionId, StageId? stageId, GameStatus? status, CancellationToken ct = default);

    /// <summary>Stages a new game for insertion.</summary>
    void Add(Game game);

    /// <summary>Stages several games for insertion (generation).</summary>
    void AddRange(IEnumerable<Game> games);

    /// <summary>Stages a game for deletion.</summary>
    void Remove(Game game);
}

/// <summary>Persistence for the frozen <see cref="GameRosterEntry"/> snapshot.</summary>
public interface IGameRosterRepository
{
    /// <summary>Lists a game's snapshot roster.</summary>
    Task<IReadOnlyList<GameRosterEntry>> ListForGameAsync(GameId gameId, CancellationToken ct = default);

    /// <summary>Stages snapshot rows for insertion.</summary>
    void AddRange(IEnumerable<GameRosterEntry> entries);
}

/// <summary>
/// Persistence for the append-only <see cref="GameEvent"/> log. Rows are only ever inserted, or have
/// <c>is_voided</c> flipped — never updated otherwise, never deleted.
/// </summary>
public interface IGameEventRepository
{
    /// <summary>Finds an event by its client-generated id (the idempotency key), or null.</summary>
    Task<GameEvent?> GetByIdAsync(GameId gameId, Guid eventId, CancellationToken ct = default);

    /// <summary>Lists a game's events in sequence order, optionally only those after a sequence.</summary>
    Task<IReadOnlyList<GameEvent>> ListForGameAsync(GameId gameId, long? afterSequence, CancellationToken ct = default);

    /// <summary>The highest sequence recorded for a game, or 0 when the log is empty.</summary>
    Task<long> GetMaxSequenceAsync(GameId gameId, CancellationToken ct = default);

    /// <summary>Stages a new event for insertion.</summary>
    void Add(GameEvent gameEvent);
}

/// <summary>Persistence for <see cref="GameOfficial"/>.</summary>
public interface IGameOfficialRepository
{
    /// <summary>Lists a game's officials.</summary>
    Task<IReadOnlyList<GameOfficial>> ListForGameAsync(GameId gameId, CancellationToken ct = default);

    /// <summary>Stages an official for insertion.</summary>
    void Add(GameOfficial official);
}

/// <summary>
/// Reads roster entries (owned by the Competitions/Registry side) for the game module — returns plain
/// rows so the game module never references another module's domain types.
/// </summary>
public interface IRosterSnapshotSource
{
    /// <summary>Fetches the given roster entries within the current tenant.</summary>
    Task<IReadOnlyList<RosterSnapshotRow>> GetEntriesByIdsAsync(IReadOnlyCollection<RosterEntryId> ids, CancellationToken ct = default);

    /// <summary>Lists a team's active roster entries.</summary>
    Task<IReadOnlyList<RosterSnapshotRow>> ListActiveForTeamAsync(CompetitionTeamId competitionTeamId, CancellationToken ct = default);
}

/// <summary>A team's display labels.</summary>
public sealed record TeamLabel(string Name, string ShortName, string? Abbreviation);

/// <summary>
/// Display labels for the game surface. A scorer's-table app labels its buttons with a name and a
/// jersey number; without this it would need one registry call per player to do so.
/// </summary>
public interface IGameLabelSource
{
    /// <summary>Display names for the given players. "First Last", or "First Middle Last" when a middle name is held.</summary>
    Task<IReadOnlyDictionary<PlayerId, string>> GetPlayerNamesAsync(IReadOnlyCollection<PlayerId> playerIds, CancellationToken ct = default);

    /// <summary>Labels for the given competition teams, resolved through the underlying team.</summary>
    Task<IReadOnlyDictionary<CompetitionTeamId, TeamLabel>> GetTeamLabelsAsync(IReadOnlyCollection<CompetitionTeamId> teamIds, CancellationToken ct = default);
}

/// <summary>Reads competition data (rules, entered teams, membership) for the game module.</summary>
public interface IGameCompetitionSource
{
    /// <summary>True if the competition exists within the current tenant.</summary>
    Task<bool> CompetitionExistsAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>The competition's current rule set, or null if the competition is missing.</summary>
    Task<RuleSet?> GetRuleSetAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>The teams entered into a competition, with seeds.</summary>
    Task<IReadOnlyList<EnteredTeam>> ListEnteredTeamsAsync(CompetitionId competitionId, CancellationToken ct = default);

    /// <summary>True if a competition team is entered in the given competition.</summary>
    Task<bool> IsTeamInCompetitionAsync(CompetitionTeamId competitionTeamId, CompetitionId competitionId, CancellationToken ct = default);
}

/// <summary>Commits staged changes for the game module.</summary>
public interface IGameRecordingUnitOfWork
{
    /// <summary>Persists all staged changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
