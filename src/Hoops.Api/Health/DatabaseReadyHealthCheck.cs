using Hoops.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hoops.Api.Health;

/// <summary>Readiness probe: the service is ready only when it can reach Postgres.</summary>
public sealed class DatabaseReadyHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;

    /// <summary>Creates the check.</summary>
    public DatabaseReadyHealthCheck(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Database unreachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}
