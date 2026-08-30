using Hoops.Modules.Registry.Contracts;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Api.Controllers.Registry;

/// <summary>Base for registry controllers — assembles the <see cref="RegistryCaller"/> for audit and the
/// platform-admin gate from the authenticated identity, the route org, and the request IP.</summary>
public abstract class RegistryControllerBase : ApiControllerBase
{
    /// <summary>The authenticated caller.</summary>
    protected ICurrentUser CurrentUser { get; }

    /// <summary>Creates the base controller.</summary>
    protected RegistryControllerBase(ICurrentUser currentUser) => CurrentUser = currentUser;

    /// <summary>Builds a caller acting under organisation <paramref name="orgId"/>.</summary>
    protected RegistryCaller CallerFor(Guid orgId) => new(
        UserId.FromGuid(CurrentUser.UserId!.Value),
        OrganisationId.FromGuid(orgId),
        CurrentUser.IsSystemAdmin,
        HttpContext.Connection.RemoteIpAddress?.ToString());

    /// <summary>Builds a platform-admin caller (org-agnostic).</summary>
    protected RegistryCaller PlatformCaller() => new(
        UserId.FromGuid(CurrentUser.UserId!.Value),
        OrganisationId.FromGuid(Guid.Empty),
        true,
        HttpContext.Connection.RemoteIpAddress?.ToString());
}
