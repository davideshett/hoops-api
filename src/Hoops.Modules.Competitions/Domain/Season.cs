using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Competitions.Domain;

/// <summary>A season — a named span of time under which an organisation runs competitions (e.g. "2025/26").</summary>
public sealed class Season : ITenantScoped, IAuditableEntity
{
    private Season()
    {
        Name = null!;
    }

    private Season(SeasonId id, OrganisationId organisationId, string name, DateOnly startsOn, DateOnly endsOn)
    {
        Id = id;
        OrganisationId = organisationId;
        Name = name;
        StartsOn = startsOn;
        EndsOn = endsOn;
    }

    /// <summary>The primary key.</summary>
    public SeasonId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>Display name, unique within the organisation (e.g. "2025/26").</summary>
    public string Name { get; private set; }

    /// <summary>First day of the season.</summary>
    public DateOnly StartsOn { get; private set; }

    /// <summary>Last day of the season.</summary>
    public DateOnly EndsOn { get; private set; }

    /// <summary>Whether the season is current.</summary>
    public bool IsActive { get; private set; } = true;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Creates a season. Throws if the date range is inverted.</summary>
    public static Season Create(OrganisationId organisationId, string name, DateOnly startsOn, DateOnly endsOn)
    {
        Guard.AgainstNullOrWhiteSpace(name);
        if (endsOn < startsOn)
        {
            throw new ArgumentException("A season cannot end before it starts.", nameof(endsOn));
        }

        return new Season(SeasonId.New(), organisationId, name.Trim(), startsOn, endsOn);
    }

    /// <summary>Updates mutable fields; null/omitted arguments leave fields unchanged.</summary>
    public void Update(string? name, DateOnly? startsOn, DateOnly? endsOn, bool? isActive)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        var newStart = startsOn ?? StartsOn;
        var newEnd = endsOn ?? EndsOn;
        if (newEnd < newStart)
        {
            throw new ArgumentException("A season cannot end before it starts.");
        }

        StartsOn = newStart;
        EndsOn = newEnd;

        if (isActive.HasValue)
        {
            IsActive = isActive.Value;
        }
    }
}
