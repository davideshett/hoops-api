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
