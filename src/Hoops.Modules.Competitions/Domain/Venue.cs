using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Competitions.Domain;

/// <summary>A venue where games are played. May host several courts.</summary>
public sealed class Venue : ITenantScoped, IAuditableEntity
{
    private Venue()
    {
        Name = null!;
        Timezone = null!;
    }

    private Venue(VenueId id, OrganisationId organisationId, string name, string timezone)
    {
        Id = id;
        OrganisationId = organisationId;
        Name = name;
        Timezone = timezone;
    }

    /// <summary>The primary key.</summary>
    public VenueId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>Display name.</summary>
    public string Name { get; private set; }

    /// <summary>Street address.</summary>
    public string? Address { get; private set; }

    /// <summary>City.</summary>
    public string? City { get; private set; }

    /// <summary>Number of courts at the venue.</summary>
    public int CourtCount { get; private set; } = 1;

    /// <summary>IANA timezone the venue operates in.</summary>
    public string Timezone { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Creates a venue.</summary>
    public static Venue Create(
        OrganisationId organisationId, string name, string? address, string? city, int courtCount, string timezone)
    {
        Guard.AgainstNullOrWhiteSpace(name);
        Guard.AgainstNullOrWhiteSpace(timezone);
        if (courtCount < 1)
        {
            throw new ArgumentException("A venue must have at least one court.", nameof(courtCount));
        }

        return new Venue(VenueId.New(), organisationId, name.Trim(), timezone.Trim())
        {
            Address = Normalise(address),
            City = Normalise(city),
            CourtCount = courtCount,
        };
    }

    /// <summary>Updates mutable fields; omitted arguments leave fields unchanged.</summary>
    public void Update(string? name, string? address, string? city, int? courtCount, string? timezone)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (address is not null)
        {
            Address = Normalise(address);
        }

        if (city is not null)
        {
            City = Normalise(city);
        }

        if (courtCount.HasValue)
        {
            if (courtCount.Value < 1)
            {
                throw new ArgumentException("A venue must have at least one court.", nameof(courtCount));
            }

            CourtCount = courtCount.Value;
        }

        if (!string.IsNullOrWhiteSpace(timezone))
        {
            Timezone = timezone.Trim();
        }
    }

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
