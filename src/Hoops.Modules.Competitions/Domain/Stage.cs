using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Competitions.Domain;

/// <summary>A stage within a competition — a group stage, a knockout round, or placement matches.</summary>
public sealed class Stage : ITenantScoped, IAuditableEntity
{
    private Stage()
    {
        Name = null!;
    }

    private Stage(StageId id, OrganisationId organisationId, CompetitionId competitionId, string name, StageType type, int sequence)
    {
        Id = id;
        OrganisationId = organisationId;
        CompetitionId = competitionId;
        Name = name;
        StageType = type;
        Sequence = sequence;
    }

    /// <summary>The primary key.</summary>
    public StageId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The competition this stage belongs to.</summary>
    public CompetitionId CompetitionId { get; private set; }

    /// <summary>Display name, e.g. "Group Stage" or "Semi-Finals".</summary>
    public string Name { get; private set; }

    /// <summary>The kind of stage.</summary>
    public StageType StageType { get; private set; }

    /// <summary>Order within the competition, unique per competition.</summary>
    public int Sequence { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Creates a stage.</summary>
    public static Stage Create(
        OrganisationId organisationId, CompetitionId competitionId, string name, StageType type, int sequence)
    {
        Guard.AgainstNullOrWhiteSpace(name);
        if (sequence < 1)
        {
            throw new ArgumentException("Stage sequence must be positive.", nameof(sequence));
        }

        return new Stage(StageId.New(), organisationId, competitionId, name.Trim(), type, sequence);
    }
}
