using Hoops.Modules.Competitions.Domain;
using Hoops.Modules.GameRecording.Application.Abstractions;
using Hoops.Modules.GameRecording.Domain;
using Hoops.Modules.Registry.Domain;
using Hoops.SharedKernel;
using Hoops.SharedKernel.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace Hoops.Infrastructure.Persistence.Repositories;

/// <summary>EF-backed <see cref="IGameRepository"/>.</summary>
public sealed class GameRepository(AppDbContext db) : IGameRepository
{
    /// <inheritdoc />
    public Task<Game?> GetAsync(GameId id, CancellationToken ct = default)
        => db.Games.FirstOrDefaultAsync(g => g.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Game>> ListForCompetitionAsync(CompetitionId competitionId, StageId? stageId, GameStatus? status, CancellationToken ct = default)
    {
        var query = db.Games.Where(g => g.CompetitionId == competitionId);
        if (stageId is { } s)
        {
            query = query.Where(g => g.StageId == s);
        }

        if (status is { } st)
        {
            query = query.Where(g => g.Status == st);
        }

        return await query.OrderBy(g => g.ScheduledAt).ToListAsync(ct);
    }

    /// <inheritdoc />
    public void Add(Game game) => db.Games.Add(game);

    /// <inheritdoc />
    public void AddRange(IEnumerable<Game> games) => db.Games.AddRange(games);

    /// <inheritdoc />
    public void Remove(Game game) => db.Games.Remove(game);
}

/// <summary>EF-backed <see cref="IGameRosterRepository"/>.</summary>
public sealed class GameRosterRepository(AppDbContext db) : IGameRosterRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<GameRosterEntry>> ListForGameAsync(GameId gameId, CancellationToken ct = default)
        => await db.GameRosterEntries.Where(e => e.GameId == gameId).OrderBy(e => e.JerseyNumber).ToListAsync(ct);

    /// <inheritdoc />
    public void AddRange(IEnumerable<GameRosterEntry> entries) => db.GameRosterEntries.AddRange(entries);
}

/// <summary>EF-backed <see cref="IGameEventRepository"/> over the append-only log.</summary>
public sealed class GameEventRepository(AppDbContext db) : IGameEventRepository
{
    /// <inheritdoc />
    public Task<GameEvent?> GetByIdAsync(GameId gameId, Guid eventId, CancellationToken ct = default)
        => db.GameEvents.FirstOrDefaultAsync(e => e.GameId == gameId && e.Id == eventId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<GameEvent>> ListForGameAsync(GameId gameId, long? afterSequence, CancellationToken ct = default)
    {
        var query = db.GameEvents.Where(e => e.GameId == gameId);
        if (afterSequence is { } after)
        {
            query = query.Where(e => e.Sequence > after);
        }

        return await query.OrderBy(e => e.Sequence).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<long> GetMaxSequenceAsync(GameId gameId, CancellationToken ct = default)
        => await db.GameEvents.Where(e => e.GameId == gameId)
            .Select(e => (long?)e.Sequence).MaxAsync(ct) ?? 0;

    /// <inheritdoc />
    public void Add(GameEvent gameEvent) => db.GameEvents.Add(gameEvent);
}

/// <summary>EF-backed <see cref="IGameOfficialRepository"/>.</summary>
public sealed class GameOfficialRepository(AppDbContext db) : IGameOfficialRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<GameOfficial>> ListForGameAsync(GameId gameId, CancellationToken ct = default)
        => await db.GameOfficials.Where(o => o.GameId == gameId).OrderBy(o => o.Role).ToListAsync(ct);

    /// <inheritdoc />
    public void Add(GameOfficial official) => db.GameOfficials.Add(official);
}

/// <summary>Reads roster entries for the game module, returning plain rows (no cross-module domain types leak).</summary>
public sealed class RosterSnapshotSource(AppDbContext db) : IRosterSnapshotSource
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<RosterSnapshotRow>> GetEntriesByIdsAsync(IReadOnlyCollection<RosterEntryId> ids, CancellationToken ct = default)
    {
        var entries = await db.RosterEntries.Where(r => ids.Contains(r.Id)).ToListAsync(ct);
        return entries.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RosterSnapshotRow>> ListActiveForTeamAsync(CompetitionTeamId competitionTeamId, CancellationToken ct = default)
    {
        var entries = await db.RosterEntries
            .Where(r => r.CompetitionTeamId == competitionTeamId && r.Status == RosterEntryStatus.Active)
            .OrderBy(r => r.JerseyNumber).ToListAsync(ct);
        return entries.Select(Map).ToList();
    }

    private static RosterSnapshotRow Map(RosterEntry r)
        => new(r.Id, r.CompetitionTeamId, r.PlayerId, r.JerseyNumber, r.Position, r.IsCaptain, r.VerifiedTier,
            r.Status == RosterEntryStatus.Active);
}

/// <summary>Reads competition data for the game module.</summary>
public sealed class GameCompetitionSource(AppDbContext db) : IGameCompetitionSource
{
    /// <inheritdoc />
    public Task<bool> CompetitionExistsAsync(CompetitionId competitionId, CancellationToken ct = default)
        => db.Competitions.AnyAsync(c => c.Id == competitionId, ct);

    /// <inheritdoc />
    public async Task<RuleSet?> GetRuleSetAsync(CompetitionId competitionId, CancellationToken ct = default)
        => await db.Competitions.Where(c => c.Id == competitionId).Select(c => c.RuleSet).FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<EnteredTeam>> ListEnteredTeamsAsync(CompetitionId competitionId, CancellationToken ct = default)
        => await db.CompetitionTeams
            .Where(t => t.CompetitionId == competitionId
                && (t.Status == CompetitionTeamStatus.Registered || t.Status == CompetitionTeamStatus.Confirmed))
            .Select(t => new EnteredTeam(t.Id, t.Seed))
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<bool> IsTeamInCompetitionAsync(CompetitionTeamId competitionTeamId, CompetitionId competitionId, CancellationToken ct = default)
        => db.CompetitionTeams.AnyAsync(t => t.Id == competitionTeamId && t.CompetitionId == competitionId, ct);
}

/// <summary>Display labels for the game surface, read across the Registry and Competitions tables.</summary>
public sealed class GameLabelSource(AppDbContext db) : IGameLabelSource
{
    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<PlayerId, string>> GetPlayerNamesAsync(
        IReadOnlyCollection<PlayerId> playerIds, CancellationToken ct = default)
    {
        // Players are platform-level (ADR-003), so there is no tenant filter to honour here; the
        // caller has already been authorised for the game these players are on.
        var rows = await db.Players
            .Where(p => playerIds.Contains(p.Id))
            .Select(p => new { p.Id, p.FirstName, p.MiddleName, p.LastName })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.Id,
            r => string.IsNullOrWhiteSpace(r.MiddleName) ? $"{r.FirstName} {r.LastName}" : $"{r.FirstName} {r.MiddleName} {r.LastName}");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<CompetitionTeamId, TeamLabel>> GetTeamLabelsAsync(
        IReadOnlyCollection<CompetitionTeamId> teamIds, CancellationToken ct = default)
    {
        // Tenant-filtered: a competition team is ordinary organisation data.
        var rows = await db.CompetitionTeams
            .Where(ct2 => teamIds.Contains(ct2.Id))
            .Join(db.Teams, ct2 => ct2.TeamId, t => t.Id, (ct2, t) => new { ct2.Id, ct2.DisplayName, t.Name, t.ShortName, t.Abbreviation })
            .ToListAsync(ct);

        // A competition may enter a team under a display name ("Awka Warriors B"); prefer it.
        return rows.ToDictionary(
            r => r.Id,
            r => new TeamLabel(string.IsNullOrWhiteSpace(r.DisplayName) ? r.Name : r.DisplayName, r.ShortName, r.Abbreviation));
    }
}
