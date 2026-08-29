using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Identity.Domain;

/// <summary>
/// A tenant on the platform. The organisation is the root of the tenant boundary: it carries an
/// <c>id</c>, never an <c>organisation_id</c>, so it is not itself <see cref="ITenantScoped"/>.
/// </summary>
public sealed class Organisation : IAuditableEntity
{
    // EF materialisation constructor.
    private Organisation()
    {
        Name = null!;
        Slug = null!;
        DefaultTimezone = null!;
        Settings = new Dictionary<string, string>();
    }

    private Organisation(OrganisationId id, string name, string slug, string? countryCode, string defaultTimezone)
    {
        Id = id;
        Name = name;
        Slug = slug;
        CountryCode = countryCode;
        DefaultTimezone = defaultTimezone;
        Settings = new Dictionary<string, string>();
    }

    /// <summary>The permanent primary key.</summary>
    public OrganisationId Id { get; private set; }

    /// <summary>Display name.</summary>
    public string Name { get; private set; }

    /// <summary>URL-safe unique handle. Case-insensitive (stored as citext).</summary>
    public string Slug { get; private set; }

    /// <summary>ISO 3166-1 alpha-2 country code, if known.</summary>
    public string? CountryCode { get; private set; }

    /// <summary>IANA timezone used as the default for competitions in this org (e.g. 'Africa/Lagos').</summary>
    public string DefaultTimezone { get; private set; }

    /// <summary>Object-storage key of the logo, if any. Never a public URL.</summary>
    public string? LogoUrl { get; private set; }

    /// <summary>Free-form org settings, persisted as jsonb.</summary>
    public Dictionary<string, string> Settings { get; private set; }

    /// <summary>Soft-delete marker; null while active.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Creates a new organisation.</summary>
    public static Organisation Create(string name, string slug, string? countryCode, string? defaultTimezone)
    {
        Guard.AgainstNullOrWhiteSpace(name);
        Guard.AgainstNullOrWhiteSpace(slug);

        var timezone = string.IsNullOrWhiteSpace(defaultTimezone) ? "UTC" : defaultTimezone.Trim();
        return new Organisation(OrganisationId.New(), name.Trim(), slug.Trim().ToLowerInvariant(), countryCode, timezone);
    }

    /// <summary>Updates mutable profile fields. Null arguments leave the corresponding field unchanged.</summary>
    public void UpdateProfile(string? name, string? countryCode, string? defaultTimezone, string? logoUrl)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (countryCode is not null)
        {
            CountryCode = string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim();
        }

        if (!string.IsNullOrWhiteSpace(defaultTimezone))
        {
            DefaultTimezone = defaultTimezone.Trim();
        }

        if (logoUrl is not null)
        {
            LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        }
    }
}
