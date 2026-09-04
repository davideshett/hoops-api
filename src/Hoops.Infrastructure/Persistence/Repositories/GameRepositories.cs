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
