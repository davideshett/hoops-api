using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Registry.Contracts;

/// <summary>
/// The acting context for a registry operation — used for the audit trail (§5A.4) and for the
/// platform-admin gate on privileged operations. Supplied by the API host from the JWT and request.
/// </summary>
/// <param name="UserId">The acting user.</param>
/// <param name="OrganisationId">The organisation the caller is acting under.</param>
/// <param name="IsPlatformAdmin">Whether the caller is a platform administrator (ADR-003).</param>
/// <param name="IpAddress">The caller's IP, recorded in the audit row.</param>
public sealed record RegistryCaller(UserId UserId, OrganisationId OrganisationId, bool IsPlatformAdmin, string? IpAddress);

/// <summary>Player registry use cases (search, identity, evidence, photos, flags, metrics, ledger).</summary>
public interface IPlayerRegistryService
{
    /// <summary>Searches the registry under the specificity, cap, and audit rules of §5A.4.</summary>
    Task<Result<IReadOnlyList<PlayerSummaryDto>>> SearchAsync(RegistryCaller caller, PlayerSearchRequest request, CancellationToken ct = default);

    /// <summary>Creates a player. A NIN collision returns 409 with the existing player's id.</summary>
    Task<Result<PlayerSummaryDto>> CreateAsync(RegistryCaller caller, CreatePlayerRequest request, CancellationToken ct = default);

    /// <summary>Fetches the limited projection of a player.</summary>
    Task<Result<PlayerSummaryDto>> GetAsync(PlayerId id, CancellationToken ct = default);

    /// <summary>Fetches sensitive fields; audited as ViewSensitive.</summary>
    Task<Result<PlayerSensitiveDto>> GetSensitiveAsync(RegistryCaller caller, PlayerId id, CancellationToken ct = default);

    /// <summary>Updates core identity; blocked once the NIN is verified.</summary>
    Task<Result<PlayerSummaryDto>> UpdateAsync(RegistryCaller caller, PlayerId id, UpdatePlayerRequest request, CancellationToken ct = default);

    /// <summary>Issues a presigned upload URL for a player's photo.</summary>
    Task<Result<PresignedUrlDto>> CreatePhotoUploadAsync(PlayerId id, string? contentType, CancellationToken ct = default);

    /// <summary>Issues a short-lived presigned read URL for a player's photo.</summary>
    Task<Result<PresignedUrlDto>> GetPhotoAsync(PlayerId id, CancellationToken ct = default);

    /// <summary>The player's cross-organisation history.</summary>
    Task<Result<PlayerHistoryDto>> GetHistoryAsync(PlayerId id, CancellationToken ct = default);

    /// <summary>Lists a player's eligibility flags.</summary>
    Task<Result<IReadOnlyList<EligibilityFlagDto>>> ListFlagsAsync(PlayerId id, CancellationToken ct = default);

    /// <summary>Raises an eligibility flag.</summary>
    Task<Result<EligibilityFlagDto>> RaiseFlagAsync(RegistryCaller caller, PlayerId id, RaiseFlagRequest request, CancellationToken ct = default);

    /// <summary>Resolves an eligibility flag.</summary>
    Task<Result> ResolveFlagAsync(EligibilityFlagId flagId, CancellationToken ct = default);

    /// <summary>Records reviewed DOB evidence, recomputing the tier.</summary>
    Task<Result<PlayerSummaryDto>> RecordDobEvidenceAsync(RegistryCaller caller, PlayerId id, DobEvidenceRequest request, CancellationToken ct = default);

    /// <summary>Verifies a NIN through the provider (platform admin), moving the player to tier 2.</summary>
    Task<Result<PlayerSummaryDto>> VerifyNinAsync(RegistryCaller caller, PlayerId id, VerifyNinRequest request, CancellationToken ct = default);

    /// <summary>Anonymises a player for NDPA erasure (platform admin) while retaining the id and statlines.</summary>
    Task<Result> AnonymiseAsync(RegistryCaller caller, PlayerId id, CancellationToken ct = default);

    /// <summary>Registry coverage and tier-distribution metrics.</summary>
    Task<Result<RegistryMetricsDto>> GetMetricsAsync(CancellationToken ct = default);

    /// <summary>Recomputes and validates the provenance ledger's hash chain.</summary>
    Task<Result<LedgerVerificationDto>> VerifyLedgerAsync(CancellationToken ct = default);
}

/// <summary>Merge-proposal queue (§5A.5). Orgs propose; platform admins approve/execute.</summary>
public interface IMergeService
{
    /// <summary>Proposes merging one player into another, with evidence.</summary>
    Task<Result<MergeProposalDto>> ProposeAsync(RegistryCaller caller, CreateMergeProposalRequest request, CancellationToken ct = default);

    /// <summary>Lists pending proposals (platform admin queue).</summary>
    Task<Result<IReadOnlyList<MergeProposalDto>>> ListProposalsAsync(CancellationToken ct = default);

    /// <summary>Approves and executes a merge — platform admin ONLY.</summary>
    Task<Result> ApproveAsync(RegistryCaller caller, MergeProposalId id, CancellationToken ct = default);

    /// <summary>Rejects a proposal — platform admin only.</summary>
    Task<Result> RejectAsync(RegistryCaller caller, MergeProposalId id, CancellationToken ct = default);
}

/// <summary>Roster entries — built from an existing playerId only (§5A.1).</summary>
public interface IRosterService
{
    /// <summary>Adds a registry player to a competition-team squad, enforcing jersey uniqueness.</summary>
    Task<Result<RosterEntryDto>> RegisterAsync(RegistryCaller caller, CompetitionTeamId competitionTeamId, RegisterRosterEntryRequest request, CancellationToken ct = default);

    /// <summary>Lists a squad's roster.</summary>
    Task<Result<IReadOnlyList<RosterEntryDto>>> ListAsync(CompetitionTeamId competitionTeamId, CancellationToken ct = default);

    /// <summary>Updates a roster entry.</summary>
    Task<Result<RosterEntryDto>> UpdateAsync(RosterEntryId id, UpdateRosterEntryRequest request, CancellationToken ct = default);

    /// <summary>Removes a player from a squad (frees the jersey number).</summary>
    Task<Result> RemoveAsync(RosterEntryId id, CancellationToken ct = default);
}
