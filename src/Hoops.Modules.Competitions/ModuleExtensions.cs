using Hoops.Modules.Competitions.Application;
using Hoops.Modules.Competitions.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Hoops.Modules.Competitions;

/// <summary>Registration for the Competitions module's application services.</summary>
public static class ModuleExtensions
{
    /// <summary>Registers the Competitions module's application services.</summary>
    public static IServiceCollection AddCompetitionsModule(this IServiceCollection services)
    {
        services.AddScoped<ISeasonService, SeasonService>();
        services.AddScoped<ICompetitionService, CompetitionService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IVenueService, VenueService>();
        return services;
    }
}
