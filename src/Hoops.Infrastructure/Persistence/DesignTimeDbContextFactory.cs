using Hoops.Infrastructure.Time;
using Hoops.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hoops.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> construct <see cref="AppDbContext"/> at design time (for <c>migrations add</c>)
/// without the full application host. Uses a no-op tenant and the real clock; the connection string is
/// only used to determine the provider, not to connect during scaffolding.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <inheritdoc />
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HOOPS_DB")
            ?? "Host=localhost;Port=5432;Database=hoops;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new NullCurrentTenant(), new SystemClock());
    }

    private sealed class NullCurrentTenant : ICurrentTenant
    {
        public Guid? OrganisationId => null;
    }
}
