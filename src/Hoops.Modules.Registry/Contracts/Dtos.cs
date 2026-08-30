using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Registry.Contracts;

// ── Search & projections ─────────────────────────────────────────────────────

/// <summary>Registry search input (§5A.4). Either a NIN, or a surname (≥3) plus a DOB or birth year.
/// The NIN is only ever accepted in the request body, never a query string.</summary>
public sealed record PlayerSearchRequest(string? Nin, string? LastName, DateOnly? DateOfBirth, int? BirthYear);

/// <summary>The limited projection returned by search and by GET player (§5A.4) — never sensitive fields.</summary>
public sealed record PlayerSummaryDto(
    PlayerId PlayerId, string FullName, DateOnly DateOfBirth, string Gender, string? PhotoUrl, string IdentityTier);

/// <summary>Sensitive player fields, returned only by the audited sensitive endpoint (§5A.4).</summary>
public sealed record PlayerSensitiveDto(
    PlayerId PlayerId,
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string IdentityTier,
    bool NinPresent,
    bool NinVerified,
    string DobEvidenceType,
    bool IsMinor,
    string? GuardianName,
    string? GuardianPhone,
    string Status);

// ── Create / update ──────────────────────────────────────────────────────────

/// <summary>Guardian consent captured at registration (required for minors, §5A.7).</summary>
public sealed record GuardianConsentInput(
    string GuardianName, string GuardianPhone, string ScopeVersion, string? EvidenceObjectKey);

/// <summary>
/// Create a player (only via roster registration, §5A.1). The NIN, if supplied, is in the body only —
/// it is hashed immediately and never stored, logged, or returned. If the player is a minor, guardian
/// consent is required or registration fails.
/// </summary>
public sealed record CreatePlayerRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Gender,
    string? MiddleName,
    string? KnownAs,
    string? Nin,
    string? Nationality,
    string? StateOfOrigin,
    GuardianConsentInput? GuardianConsent);

/// <summary>Update core identity. Blocked once the player's NIN is verified (§5A.5).</summary>
public sealed record UpdatePlayerRequest(string? FirstName, string? LastName, string? MiddleName, string? KnownAs);

/// <summary>Verify a NIN through the (stubbed) licensed provider. Body only; never a URL.</summary>
public sealed record VerifyNinRequest(string Nin);

/// <summary>Record reviewed DOB evidence, moving the identity tier.</summary>
public sealed record DobEvidenceRequest(string EvidenceType);

// ── Photos ───────────────────────────────────────────────────────────────────

/// <summary>A presigned URL and its expiry. The raw object key is never included.</summary>
public sealed record PresignedUrlDto(string Url, DateTimeOffset ExpiresAt);

// ── Eligibility flags ────────────────────────────────────────────────────────

/// <summary>Raise an eligibility flag that follows the player.</summary>
public sealed record RaiseFlagRequest(
    string FlagType, string Scope, string Reason, DateOnly StartsOn, DateOnly? EndsOn,
    OrganisationId? OrganisationId, CompetitionId? CompetitionId);

/// <summary>An eligibility flag.</summary>
public sealed record EligibilityFlagDto(
    EligibilityFlagId Id, PlayerId PlayerId, string FlagType, string Scope, string Reason,
    DateOnly StartsOn, DateOnly? EndsOn, bool Resolved);

// ── History, merge, metrics, ledger ──────────────────────────────────────────

/// <summary>One organisation's engagement with a player, over time.</summary>
public sealed record PlayerOrgLinkDto(OrganisationId OrganisationId, DateTimeOffset FirstLinkedAt, DateTimeOffset LastLinkedAt);

/// <summary>A player's cross-organisation history.</summary>
public sealed record PlayerHistoryDto(PlayerId PlayerId, IReadOnlyList<PlayerOrgLinkDto> Organisations);

/// <summary>Propose a merge (org proposes; platform admin executes).</summary>
public sealed record CreateMergeProposalRequest(PlayerId KeepId, PlayerId MergeId, string Evidence);

/// <summary>A merge proposal.</summary>
public sealed record MergeProposalDto(
    MergeProposalId Id, PlayerId KeepId, PlayerId MergeId, string Evidence, string Status, DateTimeOffset ProposedAt);

/// <summary>Registry coverage and growth metrics (§5A).</summary>
public sealed record RegistryMetricsDto(
    int TotalPlayers, int Tier0Asserted, int Tier1Documented, int Tier2NinVerified, int Minors, int WithNin);

/// <summary>The result of verifying the provenance ledger's hash chain (§5A.6).</summary>
public sealed record LedgerVerificationDto(bool IsValid, long? DivergenceSequence, int EntriesChecked);

// ── Roster entries ───────────────────────────────────────────────────────────

/// <summary>
/// Add a player to a competition-team squad. Carries a <see cref="PlayerId"/> only — never a free-text
/// name (§5A.1). A body without a valid playerId is rejected.
/// </summary>
public sealed record RegisterRosterEntryRequest(PlayerId PlayerId, string JerseyNumber, string? Position, bool IsCaptain);

/// <summary>Update a roster entry.</summary>
public sealed record UpdateRosterEntryRequest(string? JerseyNumber, string? Position, bool? IsCaptain, string? Status);

/// <summary>A roster entry.</summary>
public sealed record RosterEntryDto(
    RosterEntryId Id, CompetitionTeamId CompetitionTeamId, PlayerId PlayerId, int VerifiedTier,
    string JerseyNumber, string? Position, bool IsCaptain, string Status, DateTimeOffset RegisteredAt);
