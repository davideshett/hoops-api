using FluentAssertions;
using Hoops.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hoops.IntegrationTests;

/// <summary>
/// Guards test isolation. These tests previously ran against the developer's real database because the
/// connection-string override was applied too late under minimal hosting; the suite passed while
/// quietly writing to a live database. This asserts the app under test is pointed at the throwaway
/// container, so that failure mode can never return unnoticed.
/// </summary>
public sealed class TestIsolationTests : IntegrationTestBase
{
    public TestIsolationTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public void The_app_under_test_uses_the_throwaway_container_connection_string()
    {
        using var scope = Factory.Services.CreateScope();
        var configured = scope.ServiceProvider.GetRequiredService<IConfiguration>()
            .GetConnectionString("Postgres");

        configured.Should().Be(Factory.ContainerConnectionString,
            "the app must use the test container, never a connection string from appsettings.json");
    }

    [Fact]
    public void The_db_context_actually_connects_to_the_test_container()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var port = new Npgsql.NpgsqlConnectionStringBuilder(db.Database.GetDbConnection().ConnectionString).Port;
        var containerPort = new Npgsql.NpgsqlConnectionStringBuilder(Factory.ContainerConnectionString).Port;

        port.Should().Be(containerPort);
        port.Should().NotBe(5432, "5432 is the shared local infrastructure Postgres");
    }
}
