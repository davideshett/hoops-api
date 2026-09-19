using Hoops.Modules.Registry.Application.Abstractions;
using Hoops.Modules.Registry.Domain;
using Hoops.SharedKernel.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace Hoops.Infrastructure.Persistence.Repositories;

/// <summary>EF-backed <see cref="IPlayerRepository"/>. Players are platform-level — no tenant filter.</summary>
public sealed class PlayerRepository(AppDbContext db) : IPlayerRepository
{
    /// <inheritdoc />
    public Task<Player?> GetAsync(PlayerId id, CancellationToken ct = default)
        => db.Players.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc />
    public Task<Player?> GetByNinHmacAsync(byte[] ninHmac, CancellationToken ct = default)
        => db.Players.FirstOrDefaultAsync(p => p.NinHmac == ninHmac && p.MergedIntoId == null, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Player>> SearchAsync(
        string lastNameLower, DateOnly? dateOfBirth, int? birthYear, int limit, CancellationToken ct = default)
    {
        var query = db.Players.Where(p => p.MergedIntoId == null && EF.Functions.ILike(p.LastName, lastNameLower));
        if (dateOfBirth is { } dob)
        {
            query = query.Where(p => p.DateOfBirth == dob);
        }
        else if (birthYear is { } year)
        {
            query = query.Where(p => p.DateOfBirth.Year == year);
        }

        return await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).Take(limit).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<RegistryCounts> CountAsync(DateOnly today, CancellationToken ct = default)
    {
        var live = db.Players.Where(p => p.MergedIntoId == null);
        var minorThreshold = today.AddYears(-18);

        return new RegistryCounts(
            Total: await live.CountAsync(ct),
            Tier0: await live.CountAsync(p => p.IdentityTier == IdentityTier.Asserted, ct),
            Tier1: await live.CountAsync(p => p.IdentityTier == IdentityTier.Documented, ct),
            Tier2: await live.CountAsync(p => p.IdentityTier == IdentityTier.NinVerified, ct),
            Minors: await live.CountAsync(p => p.DateOfBirth > minorThreshold, ct),
            WithNin: await live.CountAsync(p => p.NinHmac != null, ct));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Player>> ListMergedIntoAsync(PlayerId survivorId, CancellationToken ct = default)
        => await db.Players.Where(p => p.MergedIntoId == survivorId).ToListAsync(ct);

    /// <inheritdoc />
    public void Add(Player player) => db.Players.Add(player);
}

/// <summary>EF-backed <see cref="IPlayerOrgLinkRepository"/>.</summary>
public sealed class PlayerOrgLinkRepository(AppDbContext db) : IPlayerOrgLinkRepository
{
    /// <inheritdoc />
    public Task<PlayerOrgLink?> GetAsync(PlayerId playerId, OrganisationId organisationId, CancellationToken ct = default)
        => db.PlayerOrgLinks.FirstOrDefaultAsync(l => l.PlayerId == playerId && l.OrganisationId == organisationId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlayerOrgLink>> ListForPlayerAsync(PlayerId playerId, CancellationToken ct = default)
        => await db.PlayerOrgLinks.Where(l => l.PlayerId == playerId).ToListAsync(ct);

    /// <inheritdoc />
    public void Add(PlayerOrgLink link) => db.PlayerOrgLinks.Add(link);

    /// <inheritdoc />
    public void Remove(PlayerOrgLink link) => db.PlayerOrgLinks.Remove(link);
}

/// <summary>EF-backed <see cref="IConsentRepository"/>.</summary>
public sealed class ConsentRepository(AppDbContext db) : IConsentRepository
{
    /// <inheritdoc />
    public void Add(ConsentRecord consent) => db.ConsentRecords.Add(consent);
}

/// <summary>EF-backed <see cref="IEligibilityFlagRepository"/>.</summary>
public sealed class EligibilityFlagRepository(AppDbContext db) : IEligibilityFlagRepository
{
    /// <inheritdoc />
    public Task<PlayerEligibilityFlag?> GetAsync(EligibilityFlagId id, CancellationToken ct = default)
        => db.EligibilityFlags.FirstOrDefaultAsync(f => f.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlayerEligibilityFlag>> ListForPlayerAsync(PlayerId playerId, CancellationToken ct = default)
        => await db.EligibilityFlags.Where(f => f.PlayerId == playerId).OrderByDescending(f => f.StartsOn).ToListAsync(ct);

    /// <inheritdoc />
    public void Add(PlayerEligibilityFlag flag) => db.EligibilityFlags.Add(flag);
}

/// <summary>EF-backed <see cref="IRegistryAuditRepository"/>.</summary>
public sealed class RegistryAuditRepository(AppDbContext db) : IRegistryAuditRepository
{
    /// <inheritdoc />
    public void Add(RegistryAudit audit) => db.RegistryAudits.Add(audit);
}

/// <summary>EF-backed <see cref="IRegistryLedgerRepository"/>.</summary>
public sealed class RegistryLedgerRepository(AppDbContext db) : IRegistryLedgerRepository
{
    /// <inheritdoc />
    public async Task<RegistryLedgerEntry?> GetLastAsync(CancellationToken ct = default)
    {
        // Appending is read-the-tip-then-write, so two appenders that read the same tip fork the
        // chain, and every verify from then on reports tampering that never happened. Serialise them:
        // an advisory lock held for the rest of this unit of work, released when it commits. The
        // context commits the transaction it started here in SaveChangesAsync.
        await db.BeginLedgerAppendAsync(ct);
        return await db.RegistryLedger.OrderByDescending(e => e.Sequence).FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RegistryLedgerEntry>> ListAllAsync(CancellationToken ct = default)
        => await db.RegistryLedger.OrderBy(e => e.Sequence).ToListAsync(ct);

    /// <inheritdoc />
    public void Add(RegistryLedgerEntry entry) => db.RegistryLedger.Add(entry);
}

/// <summary>EF-backed <see cref="IMergeProposalRepository"/>.</summary>
public sealed class MergeProposalRepository(AppDbContext db) : IMergeProposalRepository
{
    /// <inheritdoc />
    public Task<MergeProposal?> GetAsync(MergeProposalId id, CancellationToken ct = default)
        => db.MergeProposals.FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MergeProposal>> ListPendingAsync(CancellationToken ct = default)
        => await db.MergeProposals.Where(m => m.Status == MergeProposalStatus.Pending)
            .OrderBy(m => m.ProposedAt).ToListAsync(ct);

    /// <inheritdoc />
    public void Add(MergeProposal proposal) => db.MergeProposals.Add(proposal);
}

/// <summary>EF-backed <see cref="IRosterRepository"/>. Roster entries are tenant-scoped (auto-filtered).</summary>
public sealed class RosterRepository(AppDbContext db) : IRosterRepository
{
    /// <inheritdoc />
    public Task<RosterEntry?> GetAsync(RosterEntryId id, CancellationToken ct = default)
        => db.RosterEntries.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RosterEntry>> ListForCompetitionTeamAsync(CompetitionTeamId competitionTeamId, CancellationToken ct = default)
        => await db.RosterEntries.Where(r => r.CompetitionTeamId == competitionTeamId && r.Status != RosterEntryStatus.Removed)
            .OrderBy(r => r.JerseyNumber).ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RosterEntry>> ListForPlayerAsync(PlayerId playerId, CancellationToken ct = default)
        => await db.RosterEntries.IgnoreQueryFilters().Where(r => r.PlayerId == playerId).ToListAsync(ct);

    /// <inheritdoc />
    public Task<bool> JerseyTakenAsync(CompetitionTeamId competitionTeamId, string jerseyNumber, CancellationToken ct = default)
        => db.RosterEntries.AnyAsync(
            r => r.CompetitionTeamId == competitionTeamId && r.JerseyNumber == jerseyNumber && r.Status != RosterEntryStatus.Removed, ct);

    /// <inheritdoc />
    public Task<bool> PlayerOnRosterAsync(CompetitionTeamId competitionTeamId, PlayerId playerId, CancellationToken ct = default)
        => db.RosterEntries.AnyAsync(
            r => r.CompetitionTeamId == competitionTeamId && r.PlayerId == playerId && r.Status != RosterEntryStatus.Removed, ct);

    /// <inheritdoc />
    public Task<bool> CompetitionTeamExistsAsync(CompetitionTeamId competitionTeamId, CancellationToken ct = default)
        => db.CompetitionTeams.AnyAsync(ct2 => ct2.Id == competitionTeamId, ct);

    /// <inheritdoc />
    public void Add(RosterEntry entry) => db.RosterEntries.Add(entry);
}
