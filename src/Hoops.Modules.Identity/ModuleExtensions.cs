using Hoops.Modules.Identity.Application;
using Hoops.Modules.Identity.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Hoops.Modules.Identity;

/// <summary>
/// Registration for the Identity module's application services. Repositories, the password hasher,
/// and the access-token generator are infrastructure/host concerns registered by their own layers.
/// </summary>
public static class ModuleExtensions
{
    /// <summary>Registers the Identity module's application services.</summary>
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOrganisationService, OrganisationService>();
        return services;
    }
}
