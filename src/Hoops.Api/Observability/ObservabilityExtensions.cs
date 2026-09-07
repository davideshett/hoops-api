using System.Threading.RateLimiting;
using Hoops.Api.Http;
using Hoops.Modules.GameRecording.Application;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Hoops.Api.Observability;

/// <summary>Disambiguates Npgsql's tracing extension from EF Core's identically named DI extension.</summary>
internal static class NpgsqlTracingExtensions
{
    public static TracerProviderBuilder AddNpgsqlDatabaseTracing(this TracerProviderBuilder builder)
        => Npgsql.TracerProviderBuilderExtensions.AddNpgsql(builder);
}

/// <summary>Tracing, metrics, and rate limiting for the write path (§13, Phase 7).</summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// The rate-limit policy for event submission. Deliberately GENEROUS: a fast official at a busy
    /// scorer's table taps often, and throttling real recording is far worse than absorbing it. This
    /// exists to stop a runaway client or a retry storm, not to pace a human.
    /// </summary>
    public const string EventSubmissionPolicy = "event-submission";

    /// <summary>Registers OpenTelemetry tracing and metrics.</summary>
    public static IServiceCollection AddHoopsObservability(
        this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var otlpEndpoint = configuration["Otlp:Endpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(RecordingTelemetry.SourceName)
                    .AddAspNetCoreInstrumentation(options =>
                        // Health probes would otherwise dominate the trace volume.
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation()
                    // Fully qualified: EF Core also exposes an AddNpgsql extension, on IServiceCollection.
                    .AddNpgsqlDatabaseTracing();

                // Without a collector configured there is nothing to export to; the spans are still
                // produced, so tests and local debugging can observe them.
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return services;
    }

    /// <summary>Registers the rate limiter used by the event endpoints.</summary>
    public static IServiceCollection AddHoopsRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitsPerMinute = configuration.GetValue("RateLimiting:EventsPerMinute", 600);
        var queueLimit = configuration.GetValue("RateLimiting:EventQueueLimit", 50);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Partitioned per game, not per user: two officials working one game share a budget, and a
            // busy game never starves a quiet one on another court.
            options.AddPolicy(EventSubmissionPolicy, context =>
            {
                var gameId = context.Request.RouteValues.TryGetValue("gameId", out var value)
                    ? value?.ToString() ?? "unknown"
                    : "unknown";

                return RateLimitPartition.GetTokenBucketLimiter(gameId, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = permitsPerMinute,
                    TokensPerPeriod = permitsPerMinute,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = queueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true,
                });
            });

            // A rejection must still be a typed problem-details response, not a bare 429.
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "1";
                await ProblemJson.WriteAsync(
                    context.HttpContext, StatusCodes.Status429TooManyRequests, "RATE_LIMITED",
                    "Too Many Requests",
                    "Event submission for this game is being rate limited. Retry shortly; submission is idempotent.");
            };
        });

        return services;
    }
}
