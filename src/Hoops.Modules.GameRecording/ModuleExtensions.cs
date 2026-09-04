using Hoops.Modules.GameRecording.Application;
using Hoops.Modules.GameRecording.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Hoops.Modules.GameRecording;

/// <summary>Registration for the GameRecording module's application services.</summary>
public static class ModuleExtensions
{
    /// <summary>Registers the GameRecording module's application services.</summary>
    public static IServiceCollection AddGameRecordingModule(this IServiceCollection services)
    {
        services.AddScoped<IGameService, GameService>();
        return services;
    }
}
