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

    /// <summary>The throwaway container's connection string — the only database tests may touch.</summary>
    public string ContainerConnectionString => _postgres.GetConnectionString();

    /// <summary>Runs a command inside the database container — used by the backup/restore rehearsal.</summary>
    public async Task<(long ExitCode, string Stdout, string Stderr)> ExecInContainerAsync(params string[] command)
    {
        var result = await _postgres.ExecAsync(command);
        return (result.ExitCode ?? -1, result.Stdout ?? string.Empty, result.Stderr ?? string.Empty);
    }

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
        // Development so Swagger is served (an acceptance criterion).
        builder.UseEnvironment(Environments.Development);

        // These MUST be UseSetting, not ConfigureAppConfiguration: under minimal hosting the app reads
        // its connection string eagerly while composing services (AddInfrastructure), which happens
        // before ConfigureAppConfiguration sources are layered in. Using UseSetting puts them in host
        // configuration early enough to win — otherwise the tests silently fall back to appsettings.json
        // and run against the developer's real database.
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-at-least-32-bytes-long-000");
        builder.UseSetting("Registry:NinPepper", "integration-test-nin-pepper");
        builder.UseSetting("CaptureLogs", "true"); // in-memory sink for the no-NIN-in-logs assertion
    }
}

/// <summary>Shares one container and factory across an entire integration-test collection.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    /// <summary>The xUnit collection name.</summary>
    public const string Name = "api";
}
