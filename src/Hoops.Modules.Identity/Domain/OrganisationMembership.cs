using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Identity.Domain;

/// <summary>
/// A user's role within one organisation. This is the join that makes multi-org membership possible.
/// It is deliberately <b>not</b> <see cref="ITenantScoped"/>: it must be queryable across
/// organisations (to list every org a user belongs to at login), which a tenant filter would break.
/// It appears on the architecture test's explicit unscoped whitelist.
/// </summary>
public sealed class OrganisationMembership : IAuditableEntity
{
    // EF materialisation constructor.
    private OrganisationMembership()
    {
    }

    private OrganisationMembership(
        MembershipId id,
        OrganisationId organisationId,
        UserId userId,
        OrganisationRole role,
        DateTimeOffset acceptedAt)
    {
        Id = id;
        OrganisationId = organisationId;
        UserId = userId;
        Role = role;
        AcceptedAt = acceptedAt;
    }

    /// <summary>The permanent primary key.</summary>
    public MembershipId Id { get; private set; }

    /// <summary>The organisation this membership grants access to.</summary>
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The member.</summary>
    public UserId UserId { get; private set; }

    /// <summary>The member's role.</summary>
    public OrganisationRole Role { get; private set; }

    /// <summary>When the member was invited, if the membership began as an invitation.</summary>
    public DateTimeOffset? InvitedAt { get; private set; }

    /// <summary>When the membership became active.</summary>
    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Creates an immediately-active membership — used when a user creates an org (becoming Owner) or
    /// an admin adds an existing user directly.
    /// </summary>
    public static OrganisationMembership CreateActive(
        OrganisationId organisationId,
        UserId userId,
        OrganisationRole role,
        DateTimeOffset at)
        => new(MembershipId.New(), organisationId, userId, role, at);

    /// <summary>Changes the member's role.</summary>
    public void ChangeRole(OrganisationRole role) => Role = role;
}
