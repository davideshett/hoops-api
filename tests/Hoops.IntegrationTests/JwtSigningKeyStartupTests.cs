using FluentAssertions;
using Hoops.Api.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hoops.IntegrationTests;

/// <summary>
/// The signing key is supplied from a secret store and is deliberately absent from
/// <c>appsettings.json</c>: a value there binds in EVERY environment, so a deploy that forgot to set
/// <c>Jwt__SigningKey</c> would boot and sign tokens with a key published in this repository rather
/// than refusing to start.
///
/// These tests hold that shut from both sides — production must not start without a real secret, and
/// a developer running <c>dotnet run</c> with no secret at all must still get a working API.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class JwtSigningKeyStartupTests
{
    private readonly ApiFactory _factory;

    public JwtSigningKeyStartupTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Production_refuses_to_start_when_the_signing_key_is_absent()
    {
        await using var factory = new StartupFactory(Environments.Production, signingKey: null);

        var start = async () => await factory.StartAsync();

        (await start.Should().ThrowAsync<OptionsValidationException>())
            .Which.Failures.Should().ContainMatch("*Jwt:SigningKey must be configured*");
    }

    [Fact]
    public async Task Production_refuses_to_start_with_the_published_development_placeholder()
    {
        // Long enough to satisfy the length check, which is exactly why length alone is not enough.
        await using var factory = new StartupFactory(
            Environments.Production, JwtOptions.DevelopmentPlaceholderKey);

        var start = async () => await factory.StartAsync();

        (await start.Should().ThrowAsync<OptionsValidationException>())
            .Which.Failures.Should().ContainMatch("*development placeholder*");
    }

    [Fact]
    public async Task Production_starts_with_a_real_signing_key()
    {
        await using var factory = new StartupFactory(
            Environments.Production, "a-genuine-production-signing-key-of-sufficient-length");

        var start = async () => await factory.StartAsync();

        await start.Should().NotThrowAsync("a real secret is all production needs");
    }

    [Fact]
    public async Task Development_starts_with_no_signing_key_configured_at_all()
    {
        // The everyday `dotnet run` path: the placeholder is substituted in memory, never from a file.
        // Development migrates on boot, so this one needs the collection's throwaway container.
        await using var factory = new StartupFactory(
            Environments.Development, signingKey: null, _factory.ContainerConnectionString);

        var start = async () => await factory.StartAsync();

        await start.Should().NotThrowAsync("local development must not require a secret store");
    }

    /// <summary>
    /// Boots the real host far enough to run options validation. It never serves a request, so the
    /// connection string points nowhere by default — nothing in startup opens a connection outside
    /// Development. Development migrates on boot, so those cases pass a real one.
    /// </summary>
    private sealed class StartupFactory(string environment, string? signingKey, string? connectionString = null)
        : WebApplicationFactory<Program>
    {
        public Task StartAsync()
        {
            // Realising Services builds AND starts the host, which is what triggers ValidateOnStart.
            _ = Services;
            return Task.CompletedTask;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Registry:NinPepper", "startup-test-pepper");

            // Unreachable unless a caller supplies a real one — see the class summary.
            builder.UseSetting(
                "ConnectionStrings:Postgres",
                connectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1");

            if (signingKey is not null)
            {
                builder.UseSetting("Jwt:SigningKey", signingKey);
            }
        }
    }
}
