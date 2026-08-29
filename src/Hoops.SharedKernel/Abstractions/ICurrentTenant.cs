namespace Hoops.SharedKernel.Abstractions;

/// <summary>
/// The organisation the current request operates on, resolved from the route and validated against
/// the caller's memberships. Drives the tenant query filter in <c>AppDbContext</c>. Null on requests
/// that are not organisation-scoped (auth, registry, health).
/// </summary>
public interface ICurrentTenant
{
    /// <summary>The organisation in scope, or null when the request is not tenant-scoped.</summary>
    Guid? OrganisationId { get; }
}
