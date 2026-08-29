using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Hoops.IntegrationTests;

public sealed class AuthFlowTests : IntegrationTestBase
{
    public AuthFlowTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Register_then_login_then_me_returns_the_user()
    {
        var client = NewClient();
        var email = UniqueEmail();

        await RegisterAsync(client, email);
        var token = await LoginAsync(client, email);

        var me = await AuthenticatedClient(token).GetAsync("/api/v1/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("email").GetString().Should().Be(email);
        doc.RootElement.GetProperty("organisations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Duplicate_email_returns_409_conflict()
    {
        var client = NewClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);

        var second = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "password123", fullName = "Dup" });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemJsonAsync(second, "EMAIL_ALREADY_REGISTERED");
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var client = NewClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "wrong-password" });

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemJsonAsync(login, "INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_old_token_stops_working()
    {
        var client = NewClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "password123" });
        using var loginDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var refreshToken = loginDoc.RootElement.GetProperty("refreshToken").GetString();

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);

        // The rotated (old) token must no longer be accepted.
        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invalid_register_payload_returns_400_problem_json()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = "not-an-email", password = "short", fullName = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemJsonAsync(response, "VALIDATION_ERROR");
    }

    private static async Task AssertProblemJsonAsync(HttpResponseMessage response, string expectedCode)
    {
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be(expectedCode);
        doc.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }
}
