using Hoops.Modules.Identity.Domain;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Identity.Application.Abstractions;

/// <summary>Hashes and verifies passwords. Implemented in Infrastructure over ASP.NET Core Identity's hasher.</summary>
public interface IPasswordHasher
{
    /// <summary>Produces an opaque hash of a plaintext password.</summary>
    string Hash(string password);

    /// <summary>Verifies a plaintext password against a stored hash.</summary>
    bool Verify(string hash, string password);
}

/// <summary>A minted access token and its expiry.</summary>
/// <param name="Value">The signed JWT.</param>
/// <param name="ExpiresAt">Expiry in UTC.</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>One organisation membership to embed as a claim in an access token.</summary>
/// <param name="OrganisationId">The organisation.</param>
/// <param name="Role">The user's role there.</param>
public sealed record MembershipClaim(OrganisationId OrganisationId, OrganisationRole Role);

/// <summary>
/// Mints signed JWT access tokens. Implemented in the API host, which owns the signing key and JWT
/// configuration; declared here so the application service depends only on the abstraction.
/// </summary>
public interface IAccessTokenGenerator
{
    /// <summary>Mints an access token embedding the user's identity and organisation memberships.</summary>
    AccessToken Generate(
        UserId userId,
        string email,
        bool isSystemAdmin,
        IReadOnlyCollection<MembershipClaim> memberships);
}

/// <summary>Tunable auth parameters, bound from configuration.</summary>
/// <param name="RefreshTokenLifetime">How long an issued refresh token remains valid.</param>
public sealed record AuthOptions(TimeSpan RefreshTokenLifetime);
