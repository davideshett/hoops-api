using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Identity.Contracts;

/// <summary>Registration payload for a new platform user.</summary>
/// <param name="Email">Login email; must be unique.</param>
/// <param name="Password">Plaintext password; hashed before storage, never persisted or logged.</param>
/// <param name="FullName">Display name.</param>
public sealed record RegisterRequest(string Email, string Password, string FullName);

/// <summary>Login payload.</summary>
/// <param name="Email">Registered email.</param>
/// <param name="Password">Plaintext password to verify.</param>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Refresh payload — exchanges a valid refresh token for a fresh token pair.</summary>
/// <param name="RefreshToken">The opaque refresh token issued at login.</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Logout payload — revokes the supplied refresh token.</summary>
/// <param name="RefreshToken">The refresh token to revoke.</param>
public sealed record LogoutRequest(string RefreshToken);

/// <summary>A summary of one organisation the caller belongs to, with their role in it.</summary>
/// <param name="Id">The organisation id.</param>
/// <param name="Name">Display name.</param>
/// <param name="Slug">URL handle.</param>
/// <param name="Role">The caller's role in this organisation.</param>
public sealed record OrganisationMembershipSummary(OrganisationId Id, string Name, string Slug, string Role);

/// <summary>The result of a successful authentication.</summary>
/// <param name="AccessToken">Signed JWT bearer token.</param>
/// <param name="RefreshToken">Opaque refresh token; store securely, present to <c>/auth/refresh</c>.</param>
/// <param name="AccessTokenExpiresAt">When the access token expires, in UTC.</param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
/// <param name="Organisations">Every organisation the user belongs to, with roles.</param>
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    string TokenType,
    IReadOnlyList<OrganisationMembershipSummary> Organisations);

/// <summary>The authenticated user, returned by <c>/auth/me</c>.</summary>
/// <param name="Id">User id.</param>
/// <param name="Email">Login email.</param>
/// <param name="FullName">Display name.</param>
/// <param name="IsSystemAdmin">Whether the user is a platform administrator.</param>
/// <param name="Organisations">Every organisation the user belongs to, with roles.</param>
public sealed record CurrentUserDto(
    UserId Id,
    string Email,
    string FullName,
    bool IsSystemAdmin,
    IReadOnlyList<OrganisationMembershipSummary> Organisations);
