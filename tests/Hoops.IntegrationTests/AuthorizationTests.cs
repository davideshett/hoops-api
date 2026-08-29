using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Hoops.IntegrationTests;

public sealed class AuthorizationTests : IntegrationTestBase
{
    public AuthorizationTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Org_endpoint_without_a_token_returns_401_not_500()
    {
        var response = await NewClient().GetAsync("/api/v1/organisations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Non_member_gets_403_on_another_orgs_endpoint()
    {
        // Owner A creates an org.
        var ownerClient = NewClient();
        var ownerEmail = UniqueEmail();
        await RegisterAsync(ownerClient, ownerEmail);
        var ownerToken = await LoginAsync(ownerClient, ownerEmail);

        var created = await AuthenticatedClient(ownerToken).PostAsJsonAsync("/api/v1/organisations",
            new { name = "Anambra BBA", slug = UniqueSlug(), countryCode = "NG", defaultTimezone = "Africa/Lagos" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var orgId = createdDoc.RootElement.GetProperty("id").GetString();

        // Outsider B, a valid user with no membership, is forbidden.
        var outsiderEmail = UniqueEmail();
        var outsiderClient = NewClient();
        await RegisterAsync(outsiderClient, outsiderEmail);
        var outsiderToken = await LoginAsync(outsiderClient, outsiderEmail);

        var forbidden = await AuthenticatedClient(outsiderToken).GetAsync($"/api/v1/organisations/{orgId}");

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        forbidden.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Owner_can_read_their_own_org_after_reauthenticating()
    {
        var email = UniqueEmail();
        var client = NewClient();
        await RegisterAsync(client, email);
        var token = await LoginAsync(client, email);

        var created = await AuthenticatedClient(token).PostAsJsonAsync("/api/v1/organisations",
            new { name = "Lagos Schools League", slug = UniqueSlug(), countryCode = "NG", defaultTimezone = "Africa/Lagos" });
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var orgId = createdDoc.RootElement.GetProperty("id").GetString();

        // Re-login so the new membership is in the token's claims.
        var refreshedToken = await LoginAsync(client, email);
        var read = await AuthenticatedClient(refreshedToken).GetAsync($"/api/v1/organisations/{orgId}");

        read.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await AuthenticatedClient(refreshedToken).GetAsync("/api/v1/organisations");
        using var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        listDoc.RootElement.GetArrayLength().Should().Be(1);
        listDoc.RootElement[0].GetProperty("role").GetString().Should().Be("Owner");
    }

    [Fact]
    public async Task Owner_can_invite_a_member_and_list_members()
    {
        var ownerEmail = UniqueEmail();
        var ownerClient = NewClient();
        await RegisterAsync(ownerClient, ownerEmail);
        var ownerToken = await LoginAsync(ownerClient, ownerEmail);

        var created = await AuthenticatedClient(ownerToken).PostAsJsonAsync("/api/v1/organisations",
            new { name = "Fed", slug = UniqueSlug(), countryCode = "NG", defaultTimezone = "Africa/Lagos" });
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var orgId = createdDoc.RootElement.GetProperty("id").GetString();

        var inviteeEmail = UniqueEmail();
        await RegisterAsync(NewClient(), inviteeEmail);

        var adminToken = await LoginAsync(ownerClient, ownerEmail);
        var admin = AuthenticatedClient(adminToken);

        var invite = await admin.PostAsJsonAsync($"/api/v1/organisations/{orgId}/members/invite",
            new { email = inviteeEmail, role = "Statistician" });
        invite.StatusCode.Should().Be(HttpStatusCode.Created);

        var members = await admin.GetAsync($"/api/v1/organisations/{orgId}/members");
        using var membersDoc = JsonDocument.Parse(await members.Content.ReadAsStringAsync());
        membersDoc.RootElement.GetArrayLength().Should().Be(2);
    }
}
