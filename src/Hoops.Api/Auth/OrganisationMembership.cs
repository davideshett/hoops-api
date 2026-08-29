using System.Globalization;
using Hoops.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Hoops.Api.Auth;

/// <summary>
/// Requires the caller to be a member of the organisation named by the request's <c>orgId</c> route
/// segment, optionally holding one of a set of roles. Platform administrators satisfy any such
/// requirement.
/// </summary>
public sealed class OrganisationMembershipRequirement : IAuthorizationRequirement
{
    /// <summary>Creates the requirement. An empty role set means any role suffices.</summary>
    public OrganisationMembershipRequirement(params OrganisationRole[] allowedRoles)
        => AllowedRoles = allowedRoles;

    /// <summary>Roles that satisfy the requirement; empty means membership in any role.</summary>
    public IReadOnlyCollection<OrganisationRole> AllowedRoles { get; }
}

/// <summary>Evaluates <see cref="OrganisationMembershipRequirement"/> against the caller's claims.</summary>
public sealed class OrganisationMembershipHandler : AuthorizationHandler<OrganisationMembershipRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrganisationMembershipRequirement requirement)
    {
        // Platform admins transcend the tenant boundary (ADR-003).
        if (string.Equals(context.User.FindFirst(HoopsClaims.SystemAdmin)?.Value, "true", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.Resource is not HttpContext httpContext)
        {
            return Task.CompletedTask;
        }

        if (!httpContext.Request.RouteValues.TryGetValue(CurrentTenant.RouteKey, out var raw)
            || !Guid.TryParse(raw?.ToString(), out var orgId))
        {
            return Task.CompletedTask;
        }

        foreach (var claim in context.User.FindAll(HoopsClaims.Membership))
        {
            if (TryParseMembership(claim.Value, out var claimOrgId, out var role)
                && claimOrgId == orgId
                && (requirement.AllowedRoles.Count == 0 || requirement.AllowedRoles.Contains(role)))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }

    private static bool TryParseMembership(string value, out Guid organisationId, out OrganisationRole role)
    {
        organisationId = Guid.Empty;
        role = default;

        var separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return false;
        }

        return Guid.TryParse(value.AsSpan(0, separator), CultureInfo.InvariantCulture, out organisationId)
            && Enum.TryParse(value[(separator + 1)..], ignoreCase: true, out role)
            && Enum.IsDefined(role);
    }
}
