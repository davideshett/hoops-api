using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Competitions.Domain;

/// <summary>A pool within a group stage, e.g. "Group A".</summary>
public sealed class Group : ITenantScoped, IAuditableEntity
{
    private Group()
    {
        Name = null!;
    }

    private Group(GroupId id, OrganisationId organisationId, StageId stageId, string name)
    {
        Id = id;
        OrganisationId = organisationId;
        StageId = stageId;
        Name = name;
    }

    /// <summary>The primary key.</summary>
    public GroupId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The stage this group belongs to.</summary>
    public StageId StageId { get; private set; }

    /// <summary>Display name, e.g. "Group A".</summary>
    public string Name { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Creates a group.</summary>
    public static Group Create(OrganisationId organisationId, StageId stageId, string name)
    {
        Guard.AgainstNullOrWhiteSpace(name);
        return new Group(GroupId.New(), organisationId, stageId, name.Trim());
    }
}
