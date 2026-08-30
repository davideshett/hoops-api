using System.Text.Json.Serialization;

namespace Hoops.SharedKernel.Identifiers;

/// <summary>Identifies a <c>User</c> — a platform-level login, not owned by any organisation.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct UserId(Guid Value) : IStronglyTypedId<UserId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static UserId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static UserId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies an <c>Organisation</c> — a tenant on the platform.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct OrganisationId(Guid Value) : IStronglyTypedId<OrganisationId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static OrganisationId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static OrganisationId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies an <c>OrganisationMembership</c> — a user's role within one organisation.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct MembershipId(Guid Value) : IStronglyTypedId<MembershipId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static MembershipId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static MembershipId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>RefreshToken</c> row.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct RefreshTokenId(Guid Value) : IStronglyTypedId<RefreshTokenId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static RefreshTokenId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static RefreshTokenId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>Season</c>.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct SeasonId(Guid Value) : IStronglyTypedId<SeasonId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static SeasonId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static SeasonId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>Competition</c>.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct CompetitionId(Guid Value) : IStronglyTypedId<CompetitionId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static CompetitionId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static CompetitionId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>Stage</c> within a competition.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct StageId(Guid Value) : IStronglyTypedId<StageId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static StageId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static StageId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>Group</c> (pool) within a stage.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct GroupId(Guid Value) : IStronglyTypedId<GroupId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static GroupId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static GroupId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a canonical <c>Team</c> (club).</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct TeamId(Guid Value) : IStronglyTypedId<TeamId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static TeamId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static TeamId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>CompetitionTeam</c> — a team's entry into one competition.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct CompetitionTeamId(Guid Value) : IStronglyTypedId<CompetitionTeamId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static CompetitionTeamId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static CompetitionTeamId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>Venue</c>.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct VenueId(Guid Value) : IStronglyTypedId<VenueId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static VenueId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static VenueId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>TeamStaff</c> member.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct TeamStaffId(Guid Value) : IStronglyTypedId<TeamStaffId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static TeamStaffId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static TeamStaffId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>Player</c> — the canonical, platform-level record of a human being (ADR-003).
/// This is the only key that ever identifies a player: permanent, public, never derived from the NIN.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct PlayerId(Guid Value) : IStronglyTypedId<PlayerId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static PlayerId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static PlayerId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>PlayerOrgLink</c> — an organisation's engagement with a player.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct PlayerOrgLinkId(Guid Value) : IStronglyTypedId<PlayerOrgLinkId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static PlayerOrgLinkId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static PlayerOrgLinkId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>ConsentRecord</c>.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct ConsentRecordId(Guid Value) : IStronglyTypedId<ConsentRecordId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static ConsentRecordId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static ConsentRecordId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>PlayerEligibilityFlag</c>.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct EligibilityFlagId(Guid Value) : IStronglyTypedId<EligibilityFlagId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static EligibilityFlagId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static EligibilityFlagId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>RegistryAudit</c> row.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct RegistryAuditId(Guid Value) : IStronglyTypedId<RegistryAuditId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static RegistryAuditId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static RegistryAuditId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>MergeProposal</c>.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct MergeProposalId(Guid Value) : IStronglyTypedId<MergeProposalId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static MergeProposalId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static MergeProposalId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a <c>RosterEntry</c> — a player's participation in one competition team's squad.</summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverterFactory))]
public readonly record struct RosterEntryId(Guid Value) : IStronglyTypedId<RosterEntryId>
{
    /// <summary>Mints a new time-sortable (UUID v7) id.</summary>
    public static RosterEntryId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public static RosterEntryId FromGuid(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
