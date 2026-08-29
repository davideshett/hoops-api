using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace Hoops.IntegrationTests;

public sealed class InfrastructureEndpointTests : IntegrationTestBase
{
    public InfrastructureEndpointTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Health_endpoints_report_healthy()
    {
        var client = NewClient();

        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Swagger_document_is_served_and_declares_bearer_security()
    {
        var response = await NewClient().GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("info").GetProperty("title").GetString().Should().Be("Hoops API");
        doc.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .TryGetProperty("Bearer", out _).Should().BeTrue();
        doc.RootElement.GetProperty("paths").TryGetProperty("/api/v1/Auth/login", out _).Should().BeTrue();
    }
}
