using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Hoops.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway Postgres container (Testcontainers). Migrations are applied
/// by the app itself on startup, so a booted factory is a fully migrated system under test.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .Build();

    /// <summary>Starts the container before any test in the collection runs.</summary>
    public async Task InitializeAsync() => await _postgres.StartAsync();

    /// <summary>Stops the container after the collection completes.</summary>
    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development so Swagger is served (an acceptance criterion), plus a valid signing key and the
        // container connection string.
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-bytes-long-000",
                ["Registry:NinPepper"] = "integration-test-nin-pepper",
                ["CaptureLogs"] = "true", // enable the in-memory sink for the no-NIN-in-logs assertion
            }));
    }
}

/// <summary>Shares one container and factory across an entire integration-test collection.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    /// <summary>The xUnit collection name.</summary>
    public const string Name = "api";
}
