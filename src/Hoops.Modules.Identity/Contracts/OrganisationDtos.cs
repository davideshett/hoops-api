using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Identity.Contracts;

/// <summary>A full organisation profile.</summary>
/// <param name="Id">The organisation id.</param>
/// <param name="Name">Display name.</param>
/// <param name="Slug">URL handle.</param>
/// <param name="CountryCode">ISO 3166-1 alpha-2 country code, if set.</param>
/// <param name="DefaultTimezone">IANA default timezone.</param>
/// <param name="CreatedAt">Creation timestamp, UTC.</param>
public sealed record OrganisationDto(
    OrganisationId Id,
    string Name,
    string Slug,
    string? CountryCode,
    string DefaultTimezone,
    DateTimeOffset CreatedAt);

/// <summary>One member of an organisation.</summary>
/// <param name="UserId">The member's user id.</param>
/// <param name="Email">The member's email.</param>
/// <param name="FullName">The member's display name.</param>
/// <param name="Role">The member's role.</param>
public sealed record MemberDto(UserId UserId, string Email, string FullName, string Role);

/// <summary>Payload to create an organisation. The creator becomes its Owner.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Slug">URL handle; must be unique.</param>
/// <param name="CountryCode">Optional ISO 3166-1 alpha-2 country code.</param>
/// <param name="DefaultTimezone">Optional IANA timezone; defaults to UTC.</param>
public sealed record CreateOrganisationRequest(string Name, string Slug, string? CountryCode, string? DefaultTimezone);

/// <summary>
/// The result of creating an organisation: the new organisation, plus a freshly-minted access token
/// that already carries the caller's new Owner membership — so org-scoped endpoints authorize
/// immediately, with no second login. Keep the existing refresh token; its next use also reflects the
/// new membership.
/// </summary>
/// <param name="Id">The organisation id.</param>
/// <param name="Name">Display name.</param>
/// <param name="Slug">URL handle.</param>
/// <param name="CountryCode">ISO 3166-1 alpha-2 country code, if set.</param>
/// <param name="DefaultTimezone">IANA default timezone.</param>
/// <param name="CreatedAt">Creation timestamp, UTC.</param>
/// <param name="AccessToken">A new bearer token carrying the Owner membership. Replace your current token with this.</param>
/// <param name="AccessTokenExpiresAt">When the new access token expires, in UTC.</param>
public sealed record CreateOrganisationResponse(
    OrganisationId Id,
    string Name,
    string Slug,
    string? CountryCode,
    string DefaultTimezone,
    DateTimeOffset CreatedAt,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt);

/// <summary>Payload to update an organisation's mutable profile fields. Omitted fields are unchanged.</summary>
/// <param name="Name">New display name.</param>
/// <param name="CountryCode">New country code.</param>
/// <param name="DefaultTimezone">New default timezone.</param>
/// <param name="LogoUrl">New logo object key.</param>
public sealed record UpdateOrganisationRequest(string? Name, string? CountryCode, string? DefaultTimezone, string? LogoUrl);

/// <summary>Payload to add an existing user to an organisation by email.</summary>
/// <param name="Email">The email of an existing platform user.</param>
/// <param name="Role">The role to grant.</param>
public sealed record InviteMemberRequest(string Email, string Role);

/// <summary>Payload to change a member's role.</summary>
/// <param name="Role">The new role.</param>
public sealed record ChangeRoleRequest(string Role);
