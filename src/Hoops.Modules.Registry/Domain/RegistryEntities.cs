using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Registry.Domain;

/// <summary>
/// Which organisations have engaged a player — provenance and audit (§5A.1). Deliberately NOT
/// tenant-scoped: it is read across organisations to answer "who has this player played for".
/// </summary>
public sealed class PlayerOrgLink : IAuditableEntity
{
    private PlayerOrgLink()
    {
    }

    private PlayerOrgLink(PlayerOrgLinkId id, PlayerId playerId, OrganisationId organisationId, DateTimeOffset at)
    {
        Id = id;
        PlayerId = playerId;
        OrganisationId = organisationId;
        FirstLinkedAt = at;
        LastLinkedAt = at;
    }

    /// <summary>Primary key.</summary>
    public PlayerOrgLinkId Id { get; private set; }

    /// <summary>The player.</summary>
    public PlayerId PlayerId { get; private set; }

    /// <summary>The organisation.</summary>
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>When the org first engaged the player.</summary>
    public DateTimeOffset FirstLinkedAt { get; private set; }

    /// <summary>When the org most recently engaged the player.</summary>
    public DateTimeOffset LastLinkedAt { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Creates a link.</summary>
    public static PlayerOrgLink Create(PlayerId playerId, OrganisationId organisationId, DateTimeOffset at)
        => new(PlayerOrgLinkId.New(), playerId, organisationId, at);

    /// <summary>Touches the last-linked timestamp.</summary>
    public void Touch(DateTimeOffset at) => LastLinkedAt = at;

    /// <summary>Repoints this link to a surviving player (merge).</summary>
    public void RepointTo(PlayerId survivor) => PlayerId = survivor;
}

/// <summary>NDPA lawful-basis consent for one player (§5A.7). Registry entity — not tenant-scoped.</summary>
public sealed class ConsentRecord : IAuditableEntity
{
    private ConsentRecord()
    {
        ScopeVersion = null!;
    }

    private ConsentRecord(
        ConsentRecordId id, PlayerId playerId, ConsentType consentType, string scopeVersion,
        DateTimeOffset grantedAt, UserId capturedByUserId, string? evidenceObjectKey)
    {
        Id = id;
        PlayerId = playerId;
        ConsentType = consentType;
        ScopeVersion = scopeVersion;
        GrantedAt = grantedAt;
        CapturedByUserId = capturedByUserId;
        EvidenceObjectKey = evidenceObjectKey;
    }

    /// <summary>Primary key.</summary>
    public ConsentRecordId Id { get; private set; }

    /// <summary>The player this consent covers.</summary>
    public PlayerId PlayerId { get; private set; }

    /// <summary>Who granted the consent.</summary>
    public ConsentType ConsentType { get; private set; }

    /// <summary>Which wording was agreed, e.g. 'v1-2026-08'.</summary>
    public string ScopeVersion { get; private set; }

    /// <summary>When consent was granted.</summary>
    public DateTimeOffset GrantedAt { get; private set; }

    /// <summary>When consent was withdrawn, if it has been.</summary>
    public DateTimeOffset? WithdrawnAt { get; private set; }

    /// <summary>Private object key of a scanned signed form, if uploaded.</summary>
    public string? EvidenceObjectKey { get; private set; }

    /// <summary>The organiser who captured the consent.</summary>
    public UserId CapturedByUserId { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Records a consent grant.</summary>
    public static ConsentRecord Grant(
        PlayerId playerId, ConsentType consentType, string scopeVersion, DateTimeOffset at,
        UserId capturedByUserId, string? evidenceObjectKey)
    {
        Guard.AgainstNullOrWhiteSpace(scopeVersion);
        return new ConsentRecord(ConsentRecordId.New(), playerId, consentType, scopeVersion.Trim(), at, capturedByUserId, evidenceObjectKey);
    }
}

/// <summary>
/// An eligibility flag that follows the PLAYER across organisations (§5, §5A). Registry entity — not
/// tenant-scoped, so a platform-scope suspension binds every league.
/// </summary>
public sealed class PlayerEligibilityFlag : IAuditableEntity
{
    private PlayerEligibilityFlag()
    {
        Reason = null!;
    }

    private PlayerEligibilityFlag(
        EligibilityFlagId id, PlayerId playerId, FlagType flagType, FlagScope scope,
        string reason, DateOnly startsOn, UserId raisedByUserId)
    {
        Id = id;
        PlayerId = playerId;
        FlagType = flagType;
        Scope = scope;
        Reason = reason;
        StartsOn = startsOn;
        RaisedByUserId = raisedByUserId;
    }

    /// <summary>Primary key.</summary>
    public EligibilityFlagId Id { get; private set; }

    /// <summary>The flagged player.</summary>
    public PlayerId PlayerId { get; private set; }

    /// <summary>What kind of flag.</summary>
    public FlagType FlagType { get; private set; }

    /// <summary>How far the flag reaches.</summary>
    public FlagScope Scope { get; private set; }

    /// <summary>Bound organisation, when scope is Organisation.</summary>
    public OrganisationId? OrganisationId { get; private set; }

    /// <summary>Bound competition, when scope is Competition.</summary>
    public CompetitionId? CompetitionId { get; private set; }

    /// <summary>Why the flag was raised.</summary>
    public string Reason { get; private set; }

    /// <summary>When the flag takes effect.</summary>
    public DateOnly StartsOn { get; private set; }

    /// <summary>When the flag lapses, if bounded.</summary>
    public DateOnly? EndsOn { get; private set; }

    /// <summary>Who raised it.</summary>
    public UserId RaisedByUserId { get; private set; }

    /// <summary>When it was resolved, if it has been.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Raises a flag.</summary>
    public static PlayerEligibilityFlag Raise(
        PlayerId playerId, FlagType flagType, FlagScope scope, string reason, DateOnly startsOn,
        UserId raisedByUserId, OrganisationId? organisationId, CompetitionId? competitionId, DateOnly? endsOn)
    {
        Guard.AgainstNullOrWhiteSpace(reason);
        return new PlayerEligibilityFlag(EligibilityFlagId.New(), playerId, flagType, scope, reason.Trim(), startsOn, raisedByUserId)
        {
            OrganisationId = organisationId,
            CompetitionId = competitionId,
            EndsOn = endsOn,
        };
    }

    /// <summary>Resolves the flag.</summary>
    public void Resolve(DateTimeOffset at) => ResolvedAt ??= at;
}

/// <summary>
/// One audited registry action (§5A.4). Registry entity — not tenant-scoped. <see cref="QueryTerms"/>
/// MUST NEVER contain a raw NIN.
/// </summary>
public sealed class RegistryAudit
{
    private RegistryAudit()
    {
        QueryTerms = new Dictionary<string, string>();
    }

    private RegistryAudit(
        RegistryAuditId id, UserId actorUserId, OrganisationId organisationId, RegistryAction action,
        Dictionary<string, string> queryTerms, PlayerId? playerId, int resultCount, string? ipAddress, DateTimeOffset occurredAt)
    {
        Id = id;
        ActorUserId = actorUserId;
        OrganisationId = organisationId;
        Action = action;
        QueryTerms = queryTerms;
        PlayerId = playerId;
        ResultCount = resultCount;
        IpAddress = ipAddress;
        OccurredAt = occurredAt;
    }

    /// <summary>Primary key.</summary>
    public RegistryAuditId Id { get; private set; }

    /// <summary>The acting user.</summary>
    public UserId ActorUserId { get; private set; }

    /// <summary>The acting organisation.</summary>
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>What was done.</summary>
    public RegistryAction Action { get; private set; }

    /// <summary>Non-sensitive description of the query. NEVER a raw NIN.</summary>
    public Dictionary<string, string> QueryTerms { get; private set; }

    /// <summary>The player acted on, if applicable.</summary>
    public PlayerId? PlayerId { get; private set; }

    /// <summary>Rows returned (for searches).</summary>
    public int ResultCount { get; private set; }

    /// <summary>Caller IP.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>When it happened.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Records an audited action. Callers guarantee <paramref name="queryTerms"/> holds no raw NIN.</summary>
    public static RegistryAudit Record(
        UserId actorUserId, OrganisationId organisationId, RegistryAction action,
        Dictionary<string, string> queryTerms, DateTimeOffset occurredAt,
        PlayerId? playerId = null, int resultCount = 0, string? ipAddress = null)
        => new(RegistryAuditId.New(), actorUserId, organisationId, action, queryTerms, playerId, resultCount, ipAddress, occurredAt);
}

/// <summary>
/// A proposed merge of two player records (§5A.5). Orgs propose; platform admins approve/execute.
/// Registry-level queue — not tenant-scoped (a platform admin works it across all organisations).
/// </summary>
public sealed class MergeProposal : IAuditableEntity
{
    private MergeProposal()
    {
        Evidence = null!;
    }

    private MergeProposal(
        MergeProposalId id, PlayerId keepPlayerId, PlayerId mergePlayerId, string evidence,
        UserId proposedByUserId, OrganisationId proposedByOrganisationId, DateTimeOffset at)
    {
        Id = id;
        KeepPlayerId = keepPlayerId;
        MergePlayerId = mergePlayerId;
        Evidence = evidence;
        ProposedByUserId = proposedByUserId;
        ProposedByOrganisationId = proposedByOrganisationId;
        ProposedAt = at;
    }

    /// <summary>Primary key.</summary>
    public MergeProposalId Id { get; private set; }

    /// <summary>The surviving player.</summary>
    public PlayerId KeepPlayerId { get; private set; }

    /// <summary>The player to be folded in.</summary>
    public PlayerId MergePlayerId { get; private set; }

    /// <summary>Evidence supporting the merge.</summary>
    public string Evidence { get; private set; }

    /// <summary>Current status.</summary>
    public MergeProposalStatus Status { get; private set; } = MergeProposalStatus.Pending;

    /// <summary>Proposer.</summary>
    public UserId ProposedByUserId { get; private set; }

    /// <summary>Proposing organisation.</summary>
    public OrganisationId ProposedByOrganisationId { get; private set; }

    /// <summary>When proposed.</summary>
    public DateTimeOffset ProposedAt { get; private set; }

    /// <summary>Deciding platform admin.</summary>
    public UserId? DecidedByUserId { get; private set; }

    /// <summary>When decided.</summary>
    public DateTimeOffset? DecidedAt { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Proposes a merge of <paramref name="mergePlayerId"/> into <paramref name="keepPlayerId"/>.</summary>
    public static MergeProposal Propose(
        PlayerId keepPlayerId, PlayerId mergePlayerId, string evidence,
        UserId proposedByUserId, OrganisationId proposedByOrganisationId, DateTimeOffset at)
    {
        Guard.AgainstNullOrWhiteSpace(evidence);
        return new MergeProposal(MergeProposalId.New(), keepPlayerId, mergePlayerId, evidence.Trim(), proposedByUserId, proposedByOrganisationId, at);
    }

    /// <summary>Marks the proposal approved (execution happens in the service).</summary>
    public void Approve(UserId adminUserId, DateTimeOffset at)
    {
        Status = MergeProposalStatus.Approved;
        DecidedByUserId = adminUserId;
        DecidedAt = at;
    }

    /// <summary>Marks the proposal rejected.</summary>
    public void Reject(UserId adminUserId, DateTimeOffset at)
    {
        Status = MergeProposalStatus.Rejected;
        DecidedByUserId = adminUserId;
        DecidedAt = at;
    }
}
