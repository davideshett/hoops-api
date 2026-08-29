using Hoops.Modules.Competitions.Application.Abstractions;
using Hoops.Modules.Competitions.Domain;
using Hoops.SharedKernel.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace Hoops.Infrastructure.Persistence.Repositories;

// Every query below runs under AppDbContext's global tenant filter, so results are already
// constrained to the current organisation — the explicit org predicates are for clarity.

/// <summary>EF-backed <see cref="ISeasonRepository"/>.</summary>
public sealed class SeasonRepository(AppDbContext db) : ISeasonRepository
{
    /// <inheritdoc />
    public Task<Season?> GetAsync(SeasonId id, CancellationToken ct = default)
        => db.Seasons.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Season>> ListAsync(OrganisationId organisationId, CancellationToken ct = default)
        => await db.Seasons.Where(s => s.OrganisationId == organisationId)
            .OrderByDescending(s => s.StartsOn).ToListAsync(ct);

    /// <inheritdoc />
    public Task<bool> ExistsByNameAsync(OrganisationId organisationId, string name, CancellationToken ct = default)
        => db.Seasons.AnyAsync(s => s.OrganisationId == organisationId && s.Name == name, ct);

    /// <inheritdoc />
    public void Add(Season season) => db.Seasons.Add(season);
}

/// <summary>EF-backed <see cref="ICompetitionRepository"/>.</summary>
public sealed class CompetitionRepository(AppDbContext db) : ICompetitionRepository
{
    /// <inheritdoc />
    public Task<Competition?> GetAsync(CompetitionId id, CancellationToken ct = default)
        => db.Competitions.FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Competition>> ListAsync(
        OrganisationId organisationId, SeasonId? seasonId, CompetitionStatus? status, CancellationToken ct = default)
    {
        var query = db.Competitions.Where(c => c.OrganisationId == organisationId);
        if (seasonId is { } s)
        {
            query = query.Where(c => c.SeasonId == s);
        }

        if (status is { } st)
        {
            query = query.Where(c => c.Status == st);
        }

        return await query.OrderBy(c => c.Name).ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> ExistsBySlugAsync(OrganisationId organisationId, SeasonId seasonId, string slug, CancellationToken ct = default)
        => db.Competitions.AnyAsync(c => c.OrganisationId == organisationId && c.SeasonId == seasonId && c.Slug == slug, ct);

    /// <inheritdoc />
    public void Add(Competition competition) => db.Competitions.Add(competition);
}

/// <summary>EF-backed <see cref="IStageRepository"/>.</summary>
public sealed class StageRepository(AppDbContext db) : IStageRepository
{
    /// <inheritdoc />
    public Task<Stage?> GetAsync(StageId id, CancellationToken ct = default)
        => db.Stages.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Stage>> ListForCompetitionAsync(CompetitionId competitionId, CancellationToken ct = default)
        => await db.Stages.Where(s => s.CompetitionId == competitionId).OrderBy(s => s.Sequence).ToListAsync(ct);

    /// <inheritdoc />
    public Task<bool> ExistsBySequenceAsync(CompetitionId competitionId, int sequence, CancellationToken ct = default)
        => db.Stages.AnyAsync(s => s.CompetitionId == competitionId && s.Sequence == sequence, ct);

    /// <inheritdoc />
    public void Add(Stage stage) => db.Stages.Add(stage);
}

/// <summary>EF-backed <see cref="IGroupRepository"/>.</summary>
public sealed class GroupRepository(AppDbContext db) : IGroupRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Group>> ListForStageAsync(StageId stageId, CancellationToken ct = default)
        => await db.Groups.Where(g => g.StageId == stageId).OrderBy(g => g.Name).ToListAsync(ct);

    /// <inheritdoc />
    public void Add(Group group) => db.Groups.Add(group);
}

/// <summary>EF-backed <see cref="ITeamRepository"/>.</summary>
public sealed class TeamRepository(AppDbContext db) : ITeamRepository
{
    /// <inheritdoc />
    public Task<Team?> GetAsync(TeamId id, CancellationToken ct = default)
        => db.Teams.FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Team>> ListAsync(OrganisationId organisationId, string? search, CancellationToken ct = default)
    {
        var query = db.Teams.Where(t => t.OrganisationId == organisationId && t.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(t => EF.Functions.ILike(t.Name, pattern) || EF.Functions.ILike(t.ShortName, pattern));
        }

        return await query.OrderBy(t => t.Name).ToListAsync(ct);
    }

    /// <inheritdoc />
    public void Add(Team team) => db.Teams.Add(team);
}

/// <summary>EF-backed <see cref="ICompetitionTeamRepository"/>.</summary>
public sealed class CompetitionTeamRepository(AppDbContext db) : ICompetitionTeamRepository
{
    /// <inheritdoc />
    public Task<CompetitionTeam?> GetAsync(CompetitionTeamId id, CancellationToken ct = default)
        => db.CompetitionTeams.FirstOrDefaultAsync(ct2 => ct2.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompetitionTeam>> ListForCompetitionAsync(CompetitionId competitionId, CancellationToken ct = default)
        => await db.CompetitionTeams.Where(e => e.CompetitionId == competitionId).OrderBy(e => e.Seed).ToListAsync(ct);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(CompetitionId competitionId, TeamId teamId, CancellationToken ct = default)
        => db.CompetitionTeams.AnyAsync(e => e.CompetitionId == competitionId && e.TeamId == teamId, ct);

    /// <inheritdoc />
    public void Add(CompetitionTeam competitionTeam) => db.CompetitionTeams.Add(competitionTeam);
}

/// <summary>EF-backed <see cref="ITeamStaffRepository"/>.</summary>
public sealed class TeamStaffRepository(AppDbContext db) : ITeamStaffRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TeamStaff>> ListForCompetitionTeamAsync(CompetitionTeamId competitionTeamId, CancellationToken ct = default)
        => await db.TeamStaff.Where(s => s.CompetitionTeamId == competitionTeamId).OrderBy(s => s.FullName).ToListAsync(ct);

    /// <inheritdoc />
    public void Add(TeamStaff staff) => db.TeamStaff.Add(staff);
}

/// <summary>EF-backed <see cref="IVenueRepository"/>.</summary>
public sealed class VenueRepository(AppDbContext db) : IVenueRepository
{
    /// <inheritdoc />
    public Task<Venue?> GetAsync(VenueId id, CancellationToken ct = default)
        => db.Venues.FirstOrDefaultAsync(v => v.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Venue>> ListAsync(OrganisationId organisationId, CancellationToken ct = default)
        => await db.Venues.Where(v => v.OrganisationId == organisationId).OrderBy(v => v.Name).ToListAsync(ct);

    /// <inheritdoc />
    public void Add(Venue venue) => db.Venues.Add(venue);
}
