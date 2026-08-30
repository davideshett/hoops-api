using Hoops.Modules.Registry.Domain;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Registry.Application.Abstractions;

/// <summary>Registry coverage counts for the metrics endpoint.</summary>
/// <param name="Total">Total non-merged, non-deleted players.</param>
/// <param name="Tier0">Players at tier 0 (Asserted).</param>
/// <param name="Tier1">Players at tier 1 (Documented).</param>
/// <param name="Tier2">Players at tier 2 (NinVerified).</param>
/// <param name="Minors">Players under 18.</param>
/// <param name="WithNin">Players with a stored NIN HMAC.</param>
public sealed record RegistryCounts(int Total, int Tier0, int Tier1, int Tier2, int Minors, int WithNin);

/// <summary>Persistence for <see cref="Player"/> and registry metrics. Players are platform-level (ADR-003).</summary>
public interface IPlayerRepository
{
    /// <summary>Finds a player by id (any state), or null.</summary>
    Task<Player?> GetAsync(PlayerId id, CancellationToken ct = default);

    /// <summary>Finds an active, non-merged player by NIN HMAC (exact-match de-duplication), or null.</summary>
    Task<Player?> GetByNinHmacAsync(byte[] ninHmac, CancellationToken ct = default);

    /// <summary>
    /// Searches by surname (lowercased) and optional DOB / birth year, returning at most
    /// <paramref name="limit"/> non-merged players. NIN searches use <see cref="GetByNinHmacAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Player>> SearchAsync(string lastNameLower, DateOnly? dateOfBirth, int? birthYear, int limit, CancellationToken ct = default);

    /// <summary>Coverage and tier-distribution counts as of <paramref name="today"/>.</summary>
    Task<RegistryCounts> CountAsync(DateOnly today, CancellationToken ct = default);

    /// <summary>Lists players whose <c>merged_into_id</c> is <paramref name="survivorId"/> (transitive merge).</summary>
    Task<IReadOnlyList<Player>> ListMergedIntoAsync(PlayerId survivorId, CancellationToken ct = default);

    /// <summary>Stages a new player for insertion.</summary>
    void Add(Player player);
}

/// <summary>Persistence for <see cref="PlayerOrgLink"/>.</summary>
public interface IPlayerOrgLinkRepository
{
    /// <summary>Finds the link between a player and an organisation, or null.</summary>
    Task<PlayerOrgLink?> GetAsync(PlayerId playerId, OrganisationId organisationId, CancellationToken ct = default);

    /// <summary>Lists a player's organisation links.</summary>
    Task<IReadOnlyList<PlayerOrgLink>> ListForPlayerAsync(PlayerId playerId, CancellationToken ct = default);

    /// <summary>Stages a new link for insertion.</summary>
    void Add(PlayerOrgLink link);

    /// <summary>Stages a link for deletion (used when unioning links on merge).</summary>
    void Remove(PlayerOrgLink link);
}

/// <summary>Persistence for <see cref="ConsentRecord"/>.</summary>
public interface IConsentRepository
{
    /// <summary>Stages a new consent record for insertion.</summary>
    void Add(ConsentRecord consent);
}

/// <summary>Persistence for <see cref="PlayerEligibilityFlag"/>.</summary>
public interface IEligibilityFlagRepository
{
    /// <summary>Finds a flag by id, or null.</summary>
    Task<PlayerEligibilityFlag?> GetAsync(EligibilityFlagId id, CancellationToken ct = default);

    /// <summary>Lists a player's flags.</summary>
    Task<IReadOnlyList<PlayerEligibilityFlag>> ListForPlayerAsync(PlayerId playerId, CancellationToken ct = default);

    /// <summary>Stages a new flag for insertion.</summary>
    void Add(PlayerEligibilityFlag flag);
}

/// <summary>Persistence for <see cref="RegistryAudit"/>.</summary>
public interface IRegistryAuditRepository
{
    /// <summary>Stages an audit row for insertion.</summary>
    void Add(RegistryAudit audit);
}

/// <summary>Persistence for the hash-chained <see cref="RegistryLedgerEntry"/>.</summary>
public interface IRegistryLedgerRepository
{
    /// <summary>The most recent entry (for the previous hash), or null when the ledger is empty.</summary>
    Task<RegistryLedgerEntry?> GetLastAsync(CancellationToken ct = default);

    /// <summary>All entries in sequence order (for verification).</summary>
    Task<IReadOnlyList<RegistryLedgerEntry>> ListAllAsync(CancellationToken ct = default);

    /// <summary>Stages a ledger entry for insertion.</summary>
    void Add(RegistryLedgerEntry entry);
}

/// <summary>Persistence for <see cref="MergeProposal"/>.</summary>
public interface IMergeProposalRepository
{
    /// <summary>Finds a proposal by id, or null.</summary>
    Task<MergeProposal?> GetAsync(MergeProposalId id, CancellationToken ct = default);

    /// <summary>Lists pending proposals.</summary>
    Task<IReadOnlyList<MergeProposal>> ListPendingAsync(CancellationToken ct = default);

    /// <summary>Stages a new proposal for insertion.</summary>
    void Add(MergeProposal proposal);
}

/// <summary>Persistence for <see cref="RosterEntry"/> (tenant-scoped) plus competition-team existence.</summary>
public interface IRosterRepository
{
    /// <summary>Finds a roster entry by id, or null.</summary>
    Task<RosterEntry?> GetAsync(RosterEntryId id, CancellationToken ct = default);

    /// <summary>Lists a squad's roster entries.</summary>
    Task<IReadOnlyList<RosterEntry>> ListForCompetitionTeamAsync(CompetitionTeamId competitionTeamId, CancellationToken ct = default);

    /// <summary>Lists every roster entry for a player (used to repoint on merge).</summary>
    Task<IReadOnlyList<RosterEntry>> ListForPlayerAsync(PlayerId playerId, CancellationToken ct = default);

    /// <summary>True if a non-removed entry already uses this jersey number in the squad.</summary>
    Task<bool> JerseyTakenAsync(CompetitionTeamId competitionTeamId, string jerseyNumber, CancellationToken ct = default);

    /// <summary>True if the player is already on the squad.</summary>
    Task<bool> PlayerOnRosterAsync(CompetitionTeamId competitionTeamId, PlayerId playerId, CancellationToken ct = default);

    /// <summary>True if the competition-team exists within the current tenant.</summary>
    Task<bool> CompetitionTeamExistsAsync(CompetitionTeamId competitionTeamId, CancellationToken ct = default);

    /// <summary>Stages a new roster entry for insertion.</summary>
    void Add(RosterEntry entry);
}

/// <summary>Commits staged changes for the registry module.</summary>
public interface IRegistryUnitOfWork
{
    /// <summary>Persists all staged changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
