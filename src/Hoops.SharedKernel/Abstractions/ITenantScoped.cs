using Hoops.SharedKernel.Identifiers;

namespace Hoops.SharedKernel.Abstractions;

/// <summary>
/// Marks an entity as owned by exactly one organisation (tenant). Every implementation is given a
/// global query filter by <c>AppDbContext</c> so a forgotten <c>.Where()</c> can never leak data
/// across tenants. An architecture test fails the build if any implementation lacks a filter, and
/// if any mapped entity is neither <see cref="ITenantScoped"/> nor on the explicit unscoped
/// whitelist (ADR-003 / ADR-005).
/// </summary>
public interface ITenantScoped
{
    /// <summary>
    /// The owning organisation. Never mutated after creation. Typed (rather than the doc's
    /// illustrative <c>Guid</c>) so tenancy comparisons stay type-safe end to end.
    /// </summary>
    OrganisationId OrganisationId { get; }
}
