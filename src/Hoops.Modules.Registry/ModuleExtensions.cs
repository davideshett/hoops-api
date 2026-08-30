using Hoops.Modules.Registry.Application;
using Hoops.Modules.Registry.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Hoops.Modules.Registry;

/// <summary>Registration for the Registry module's application services.</summary>
public static class ModuleExtensions
{
    /// <summary>Registers the Registry module's application services.</summary>
    public static IServiceCollection AddRegistryModule(this IServiceCollection services)
    {
        services.AddScoped<IPlayerRegistryService, PlayerRegistryService>();
        services.AddScoped<IMergeService, MergeService>();
        services.AddScoped<IRosterService, RosterService>();
        return services;
    }
}
