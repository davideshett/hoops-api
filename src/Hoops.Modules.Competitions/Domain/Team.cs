using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Competitions.Domain;

/// <summary>
/// A canonical club, owned by an organisation and reusable across its competitions and seasons. A team
/// is entered into a competition through a <see cref="CompetitionTeam"/>; the club record itself
/// appears once.
/// </summary>
public sealed class Team : ITenantScoped, IAuditableEntity
{
    private Team()
    {
        Name = null!;
        ShortName = null!;
    }

    private Team(TeamId id, OrganisationId organisationId, string name, string shortName)
    {
        Id = id;
        OrganisationId = organisationId;
        Name = name;
        ShortName = shortName;
    }

    /// <summary>The primary key.</summary>
    public TeamId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>Full club name.</summary>
    public string Name { get; private set; }

    /// <summary>Short name for scoreboards (≤ 12 chars).</summary>
    public string ShortName { get; private set; }

    /// <summary>Three-letter abbreviation, e.g. "LAG".</summary>
    public string? Abbreviation { get; private set; }

    /// <summary>Object-storage key of the logo. Never a public URL.</summary>
    public string? LogoUrl { get; private set; }

    /// <summary>Primary jersey colour (hex); drives the recording UI's jersey buttons.</summary>
    public string? PrimaryColour { get; private set; }

    /// <summary>Secondary jersey colour (hex).</summary>
    public string? SecondaryColour { get; private set; }

    /// <summary>Optional home venue.</summary>
    public VenueId? HomeVenueId { get; private set; }

    /// <summary>Soft-delete marker; null while active.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Creates a canonical team.</summary>
    public static Team Create(
        OrganisationId organisationId, string name, string shortName,
        string? abbreviation, string? primaryColour, string? secondaryColour, VenueId? homeVenueId)
    {
        Guard.AgainstNullOrWhiteSpace(name);
        Guard.AgainstNullOrWhiteSpace(shortName);
        Guard.AgainstTooLong(shortName.Trim(), 12);

        return new Team(TeamId.New(), organisationId, name.Trim(), shortName.Trim())
        {
            Abbreviation = Normalise(abbreviation),
            PrimaryColour = Normalise(primaryColour),
            SecondaryColour = Normalise(secondaryColour),
            HomeVenueId = homeVenueId,
        };
    }

    /// <summary>Updates mutable fields; omitted arguments leave fields unchanged.</summary>
    public void Update(
        string? name, string? shortName, string? abbreviation,
        string? primaryColour, string? secondaryColour, VenueId? homeVenueId)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(shortName))
        {
            ShortName = Guard.AgainstTooLong(shortName.Trim(), 12);
        }

        if (abbreviation is not null)
        {
            Abbreviation = Normalise(abbreviation);
        }

        if (primaryColour is not null)
        {
            PrimaryColour = Normalise(primaryColour);
        }

        if (secondaryColour is not null)
        {
            SecondaryColour = Normalise(secondaryColour);
        }

        if (homeVenueId.HasValue)
        {
            HomeVenueId = homeVenueId;
        }
    }

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
