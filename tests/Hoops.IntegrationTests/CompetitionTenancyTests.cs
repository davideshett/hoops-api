using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Hoops.IntegrationTests;

/// <summary>
/// The load-bearing Phase 2 guarantee: organisation A can neither read nor mutate any organisation B
/// entity, through any endpoint. Two defences combine — the membership policy (403 on B's own route)
/// and the global tenant query filter (404 when B's id is probed through A's route).
/// </summary>
public sealed class CompetitionTenancyTests : IntegrationTestBase
{
    public CompetitionTenancyTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Org_A_cannot_read_or_mutate_org_B_entities_through_any_endpoint()
    {
        var (tokenA, orgA) = await NewOrgWithOwnerAsync();
        var (tokenB, orgB) = await NewOrgWithOwnerAsync();
        var a = AuthenticatedClient(tokenA);
        var b = AuthenticatedClient(tokenB);

        // Org B builds some data.
        var seasonB = (await ReadId(await b.PostAsJsonAsync($"/api/v1/organisations/{orgB}/seasons",
            new { name = "2025/26", startsOn = "2025-10-01", endsOn = "2026-06-30" })));
        var compB = await ReadId(await b.PostAsJsonAsync($"/api/v1/organisations/{orgB}/competitions",
            new { seasonId = seasonB, name = "B Cup", slug = "b-cup", format = "League", timezone = "Africa/Lagos" }));
        var teamB = await ReadId(await b.PostAsJsonAsync($"/api/v1/organisations/{orgB}/teams",
            new { name = "B Team", shortName = "BTeam" }));
        var venueB = await ReadId(await b.PostAsJsonAsync($"/api/v1/organisations/{orgB}/venues",
            new { name = "B Arena", timezone = "Africa/Lagos" }));

        // ── Via B's own route: A is not a member → 403 on every resource ──────────
        await Assert403(a.GetAsync($"/api/v1/organisations/{orgB}/competitions/{compB}"));
        await Assert403(a.GetAsync($"/api/v1/organisations/{orgB}/competitions"));
        await Assert403(a.GetAsync($"/api/v1/organisations/{orgB}/teams/{teamB}"));
        await Assert403(a.GetAsync($"/api/v1/organisations/{orgB}/seasons/{seasonB}"));
        await Assert403(a.GetAsync($"/api/v1/organisations/{orgB}/venues/{venueB}"));
        await Assert403(a.PatchAsJsonAsync($"/api/v1/organisations/{orgB}/competitions/{compB}", new { name = "hijacked" }));
        await Assert403(a.PatchAsJsonAsync($"/api/v1/organisations/{orgB}/teams/{teamB}", new { name = "hijacked" }));

        // ── Via A's own route, probing B's ids: the tenant filter hides them → 404 ─
        await Assert404(a.GetAsync($"/api/v1/organisations/{orgA}/competitions/{compB}"));
        await Assert404(a.GetAsync($"/api/v1/organisations/{orgA}/teams/{teamB}"));
        await Assert404(a.GetAsync($"/api/v1/organisations/{orgA}/seasons/{seasonB}"));
        await Assert404(a.GetAsync($"/api/v1/organisations/{orgA}/venues/{venueB}"));
        await Assert404(a.PatchAsJsonAsync($"/api/v1/organisations/{orgA}/competitions/{compB}", new { name = "hijacked" }));
        await Assert404(a.PatchAsJsonAsync($"/api/v1/organisations/{orgA}/teams/{teamB}", new { name = "hijacked" }));

        // ── A's own listing never contains B's data ──────────────────────────────
        var aTeams = await a.GetAsync($"/api/v1/organisations/{orgA}/teams");
        (await ReadJson(aTeams)).GetArrayLength().Should().Be(0, "A created no teams of its own");

        // ── And B's data is untouched ────────────────────────────────────────────
        var stillThere = await b.GetAsync($"/api/v1/organisations/{orgB}/competitions/{compB}");
        (await ReadJson(stillThere)).GetProperty("name").GetString().Should().Be("B Cup");
    }

    private static async Task Assert403(Task<HttpResponseMessage> call)
        => (await call).StatusCode.Should().Be(HttpStatusCode.Forbidden);

    private static async Task Assert404(Task<HttpResponseMessage> call)
        => (await call).StatusCode.Should().Be(HttpStatusCode.NotFound);

    private static async Task<string> ReadId(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadJson(response)).GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
