using Hoops.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authorization;

namespace Hoops.Api.Auth;

/// <summary>Named authorization policies. Every controller action references one of these explicitly.</summary>
public static class AuthPolicies
{
    /// <summary>Any authenticated user.</summary>
    public const string Authenticated = "Authenticated";

    /// <summary>A member of the route's organisation, in any role.</summary>
    public const string OrgMember = "OrgMember";

    /// <summary>An Owner or Admin of the route's organisation.</summary>
    public const string OrgAdmin = "OrgAdmin";

    /// <summary>An Owner, Admin, or CompetitionManager of the route's organisation. Manages competition data.</summary>
    public const string OrgManager = "OrgManager";

    /// <summary>A platform administrator (ADR-003): merge approval, NIN verification, anonymisation.</summary>
    public const string PlatformAdmin = "PlatformAdmin";

    /// <summary>Anyone who may record at the scorer's table: Owner, Admin, CompetitionManager, or Statistician.</summary>
    public const string OrgStatistician = "OrgStatistician";

    /// <summary>Registers all platform authorization policies and the membership handler.</summary>
    public static IServiceCollection AddHoopsAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, OrganisationMembershipHandler>();

        services.AddAuthorizationBuilder()
            .AddPolicy(Authenticated, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(OrgMember, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new OrganisationMembershipRequirement());
            })
            .AddPolicy(OrgAdmin, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new OrganisationMembershipRequirement(
                    OrganisationRole.Owner, OrganisationRole.Admin));
            })
            .AddPolicy(OrgManager, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new OrganisationMembershipRequirement(
                    OrganisationRole.Owner, OrganisationRole.Admin, OrganisationRole.CompetitionManager));
            })
            .AddPolicy(OrgStatistician, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new OrganisationMembershipRequirement(
                    OrganisationRole.Owner, OrganisationRole.Admin,
                    OrganisationRole.CompetitionManager, OrganisationRole.Statistician));
            })
            .AddPolicy(PlatformAdmin, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(HoopsClaims.SystemAdmin, "true");
            });

        return services;
    }
}
