using Hoops.Modules.Statistics.Application;
using Hoops.Modules.Statistics.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Hoops.Modules.Statistics;

/// <summary>Registration for the Statistics module's application services.</summary>
public static class ModuleExtensions
{
    /// <summary>Registers the Statistics module's application services.</summary>
    public static IServiceCollection AddStatisticsModule(this IServiceCollection services)
    {
        services.AddScoped<IStatisticsRecomputeService, StatisticsRecomputeService>();
        services.AddScoped<IStatisticsQueryService, StatisticsQueryService>();
        return services;
    }
}
