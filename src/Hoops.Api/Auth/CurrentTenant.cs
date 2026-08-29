using Hoops.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Hoops.Api.Auth;

/// <summary>
/// Resolves <see cref="ICurrentTenant"/> from the <c>orgId</c> route value, so the organisation in the
/// URL path drives the tenant query filter. Null on requests with no <c>orgId</c> segment. Membership
/// in that organisation is enforced separately by the authorization policy.
/// </summary>
public sealed class CurrentTenant : ICurrentTenant
{
    /// <summary>The route-value key carrying the organisation id.</summary>
    public const string RouteKey = "orgId";

    private readonly IHttpContextAccessor _accessor;

    /// <summary>Creates the accessor.</summary>
    public CurrentTenant(IHttpContextAccessor accessor) => _accessor = accessor;

    /// <inheritdoc />
    public Guid? OrganisationId
    {
        get
        {
            var raw = _accessor.HttpContext?.Request.RouteValues.TryGetValue(RouteKey, out var value) == true
                ? value?.ToString()
                : null;
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }
}
