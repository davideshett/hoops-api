using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Competitions.Domain;

/// <summary>
/// A competition run by an organisation within a season. Carries its own <see cref="RuleSet"/>
/// (stored as jsonb) which later phases snapshot onto each game at roster lock.
/// </summary>
public sealed class Competition : ITenantScoped, IAuditableEntity
{
    private Competition()
    {
        Name = null!;
        Slug = null!;
        Timezone = null!;
        RuleSet = RuleSet.Fiba();
    }

    private Competition(
        CompetitionId id,
        OrganisationId organisationId,
        SeasonId seasonId,
        string name,
        string slug,
        CompetitionFormat format,
        string? category,
        RuleSet ruleSet,
        string timezone)
    {
        Id = id;
        OrganisationId = organisationId;
        SeasonId = seasonId;
        Name = name;
        Slug = slug;
        Format = format;
        Category = category;
        RuleSet = ruleSet;
        Timezone = timezone;
    }

    /// <summary>The primary key.</summary>
    public CompetitionId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The season this competition belongs to.</summary>
    public SeasonId SeasonId { get; private set; }

    /// <summary>Display name.</summary>
    public string Name { get; private set; }

    /// <summary>URL handle, unique within (organisation, season).</summary>
    public string Slug { get; private set; }

    /// <summary>Competitive format.</summary>
    public CompetitionFormat Format { get; private set; }

    /// <summary>Free-text category, e.g. "U18 Boys" or "Senior Women".</summary>
    public string? Category { get; private set; }

    /// <summary>The rules this competition is played under.</summary>
    public RuleSet RuleSet { get; private set; }

    /// <summary>Lifecycle status.</summary>
    public CompetitionStatus Status { get; private set; } = CompetitionStatus.Draft;

    /// <summary>Optional first day.</summary>
    public DateOnly? StartsOn { get; private set; }

    /// <summary>Optional last day.</summary>
    public DateOnly? EndsOn { get; private set; }

    /// <summary>IANA timezone the competition is scheduled in.</summary>
    public string Timezone { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Creates a competition in <see cref="CompetitionStatus.Draft"/>.</summary>
    public static Competition Create(
        OrganisationId organisationId,
        SeasonId seasonId,
        string name,
        string slug,
        CompetitionFormat format,
        string? category,
        RuleSet? ruleSet,
        string timezone)
    {
        Guard.AgainstNullOrWhiteSpace(name);
        Guard.AgainstNullOrWhiteSpace(slug);
        Guard.AgainstNullOrWhiteSpace(timezone);

        return new Competition(
            CompetitionId.New(), organisationId, seasonId, name.Trim(),
            slug.Trim().ToLowerInvariant(), format, Normalise(category), ruleSet ?? RuleSet.Fiba(), timezone.Trim());
    }

    /// <summary>Updates mutable profile fields; omitted arguments leave fields unchanged.</summary>
    public void UpdateProfile(
        string? name, string? category, CompetitionFormat? format,
        RuleSet? ruleSet, DateOnly? startsOn, DateOnly? endsOn, string? timezone)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (category is not null)
        {
            Category = Normalise(category);
        }

        if (format.HasValue)
        {
            Format = format.Value;
        }

        if (ruleSet is not null)
        {
            RuleSet = ruleSet;
        }

        if (startsOn.HasValue)
        {
            StartsOn = startsOn;
        }

        if (endsOn.HasValue)
        {
            EndsOn = endsOn;
        }

        if (!string.IsNullOrWhiteSpace(timezone))
        {
            Timezone = timezone.Trim();
        }
    }

    /// <summary>Moves the competition to a new status. Returns false if the transition is not allowed.</summary>
    public bool TransitionTo(CompetitionStatus target)
    {
        var allowed = Status switch
        {
            CompetitionStatus.Draft => target is CompetitionStatus.Registration or CompetitionStatus.Archived,
            CompetitionStatus.Registration => target is CompetitionStatus.InProgress or CompetitionStatus.Archived,
            CompetitionStatus.InProgress => target is CompetitionStatus.Completed or CompetitionStatus.Archived,
            CompetitionStatus.Completed => target is CompetitionStatus.Archived,
            _ => false,
        };

        if (!allowed)
        {
            return false;
        }

        Status = target;
        return true;
    }

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
