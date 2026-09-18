using Hoops.Modules.GameRecording.Domain;
using Hoops.Modules.Statistics.Application.Abstractions;
using Hoops.Modules.Statistics.Domain;
using Hoops.SharedKernel.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace Hoops.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read models for the historical query surface. Shot charts and play-by-play read the event log
/// directly, because they need per-event detail that no aggregate carries; everything else reads the
/// derived tables.
/// </summary>
public sealed class HistoryReadRepository(AppDbContext db) : IHistoryReadRepository
{
    private static readonly string[] ShotTypes = [EventTypes.FieldGoalMade, EventTypes.FieldGoalMissed];

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlayRow>> ListPlaysAsync(
        GameId gameId, int? period, CancellationToken ct = default)
    {
        var query = db.GameEvents.Where(e => e.GameId == gameId);
        if (period is { } p)
        {
            query = query.Where(e => e.Period == p);
        }

        // Events address players by their frozen game-roster row, so resolve to registry ids for the
        // feed — the client wants to link to a player, not to a roster snapshot.
        var rows = await query
            .OrderBy(e => e.Sequence)
            .Select(e => new
            {
                e.Sequence, e.Period, e.GameClockMs, e.EventType, e.EventSubtype, e.CompetitionTeamId,
                e.Points, e.ShotZone, e.ShotDistanceCm, e.IsVoided,
                PlayerId = db.GameRosterEntries
                    .Where(r => r.Id == e.GameRosterEntryId).Select(r => (PlayerId?)r.PlayerId).FirstOrDefault(),
                SecondaryPlayerId = db.GameRosterEntries
                    .Where(r => r.Id == e.SecondaryRosterEntryId).Select(r => (PlayerId?)r.PlayerId).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return rows.Select(r => new PlayRow(
            r.Sequence, r.Period, r.GameClockMs, r.EventType, r.EventSubtype, r.CompetitionTeamId,
            r.PlayerId, r.SecondaryPlayerId, r.Points, r.ShotZone.ToString(), r.ShotDistanceCm, r.IsVoided)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShotRow>> ListGameShotsAsync(
        GameId gameId, CompetitionTeamId? teamId, PlayerId? playerId, CancellationToken ct = default)
    {
        // ONE game's chart is ordinary tenant data, so it keeps the global filter: a caller probing
        // another organisation's game id through their own route sees nothing.
        var query = ShotQuery(db.GameEvents).Where(e => e.GameId == gameId);
        if (teamId is { } team)
        {
            query = query.Where(e => e.CompetitionTeamId == team);
        }

        var rows = await ProjectShots(query, db.GameRosterEntries, playerId).ToListAsync(ct);
        return Materialise(rows);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShotRow>> ListPlayerShotsAsync(
        PlayerId playerId, CompetitionId? competitionId, CancellationToken ct = default)
    {
        // A career chart follows the PLAYER across organisations, exactly as career aggregates do
        // (ADR-003), so this read — and only this read — is deliberately untenanted. The roster lookup
        // must be untenanted too, or the shots would be found and then fail to resolve to a player.
        var query = ShotQuery(db.GameEvents.IgnoreQueryFilters());
        if (competitionId is { } competition)
        {
            var gameIds = db.Games.IgnoreQueryFilters()
                .Where(g => g.CompetitionId == competition).Select(g => g.Id);
            query = query.Where(e => gameIds.Contains(e.GameId));
        }

        var rows = await ProjectShots(query, db.GameRosterEntries.IgnoreQueryFilters(), playerId)
            .ToListAsync(ct);
        return Materialise(rows);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(CompetitionTeamId TeamId, IReadOnlyList<PlayerId> Players, int Seconds, int For, int Against)>>
        ListLineupsAsync(GameId gameId, CancellationToken ct = default)
    {
        // One game's lineups are ordinary tenant data, so the global filter stays on.
        var stints = await db.LineupStints
            .Where(s => s.GameId == gameId).ToListAsync(ct);

        // Stints store the lineup as comma-joined game-roster ids; resolve them to registry players.
        var roster = await db.GameRosterEntries
            .Where(r => r.GameId == gameId)
            .ToDictionaryAsync(r => r.Id.Value, r => r.PlayerId, ct);

        return stints.Select(s => (
            s.CompetitionTeamId,
            (IReadOnlyList<PlayerId>)s.PlayerIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => Guid.TryParse(id, out var g) && roster.TryGetValue(g, out var pid) ? pid : (PlayerId?)null)
                .Where(p => p.HasValue).Select(p => p!.Value).ToList(),
            s.SecondsPlayed,
            s.PointsFor,
            s.PointsAgainst)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlayerGameStatline>> ListStatlinesForOrganisationAsync(
        OrganisationId organisationId, CancellationToken ct = default)
        => await db.PlayerGameStatlines.IgnoreQueryFilters()
            .Where(s => s.OrganisationId == organisationId).ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompetitionPlayerAggregate>> ListAggregatesForPlayerAsync(
        PlayerId playerId, CancellationToken ct = default)
        // A career spans organisations, so this read is deliberately untenanted (ADR-003).
        => await db.CompetitionPlayerAggregates.IgnoreQueryFilters()
            .Where(a => a.PlayerId == playerId).ToListAsync(ct);

    // The caller supplies the source so it, not this helper, decides whether the tenant filter applies.
    private static IQueryable<GameEvent> ShotQuery(IQueryable<GameEvent> source)
        => source.Where(e => !e.IsVoided && ShotTypes.Contains(e.EventType)
            && e.ShotXCm != null && e.ShotYCm != null);

    // Events address players by their frozen game-roster row, so the roster source travels with the
    // event source: each caller scopes BOTH the same way, and a tenanted chart cannot end up reading
    // untenanted rosters (or the reverse) by accident.
    private static IQueryable<ShotProjection> ProjectShots(
        IQueryable<GameEvent> query, IQueryable<GameRosterEntry> roster, PlayerId? playerId)
    {
        if (playerId is { } id)
        {
            // Filtered on the EVENT, before projecting. Filtering the projection instead reads more
            // naturally but does not translate: PlayerId is itself a subquery, and EF cannot push a
            // predicate back through it — it throws at translation time rather than falling back.
            query = query.Where(e => roster
                .Where(r => r.Id == e.GameRosterEntryId)
                .Select(r => (PlayerId?)r.PlayerId)
                .FirstOrDefault() == id);
        }

        return query.Select(e => new ShotProjection(
            e.GameId,
            roster.Where(r => r.Id == e.GameRosterEntryId)
                .Select(r => (PlayerId?)r.PlayerId).FirstOrDefault(),
            e.CompetitionTeamId,
            e.EventType == EventTypes.FieldGoalMade,
            e.Points,
            e.ShotXCm!.Value,
            e.ShotYCm!.Value,
            e.ShotZone,
            e.ShotDistanceCm,
            e.Period,
            e.GameClockMs));
    }

    private static IReadOnlyList<ShotRow> Materialise(IEnumerable<ShotProjection> rows)
        => rows.Select(r => new ShotRow(
            r.GameId, r.PlayerId, r.CompetitionTeamId, r.Made,
            r.Points ?? (r.Made ? 2 : 0), r.XCm, r.YCm,
            r.Zone?.ToString() ?? "Unknown", r.DistanceCm ?? 0, r.Period, r.GameClockMs)).ToList();

    private sealed record ShotProjection(
        GameId GameId, PlayerId? PlayerId, CompetitionTeamId? CompetitionTeamId, bool Made, int? Points,
        int XCm, int YCm, Hoops.SharedKernel.ShotZone? Zone, int? DistanceCm, int Period, int GameClockMs);
}
