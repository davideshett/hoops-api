using Hoops.Modules.GameRecording.Application;
using Hoops.Modules.GameRecording.Contracts;
using Hoops.Modules.GameRecording.Domain.Projection;
using Microsoft.Extensions.DependencyInjection;

namespace Hoops.Modules.GameRecording;

/// <summary>Registration for the GameRecording module's application services.</summary>
public static class ModuleExtensions
{
    /// <summary>Registers the GameRecording module's application services.</summary>
    public static IServiceCollection AddGameRecordingModule(this IServiceCollection services)
    {
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IEventRecordingService, EventRecordingService>();

        // The projector is pure and stateless, so a singleton is safe and avoids per-request churn.
        services.AddSingleton<IGameProjector, GameProjector>();
        return services;
    }
}
