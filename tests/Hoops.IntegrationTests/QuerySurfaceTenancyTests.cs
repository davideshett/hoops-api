using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Hoops.IntegrationTests;

/// <summary>
/// The tenancy guarantee extended to the Phase 5–6 surface. <see cref="CompetitionTenancyTests"/>
/// covers the Phase 2 entities; this covers the statistics and history endpoints, where the reads are
/// keyed by a game or competition id rather than by an organisation and so are the easiest place to
/// lose the tenant boundary by reaching for <c>IgnoreQueryFilters</c>.
///
/// Each case is asserted twice: organisation B sees its own data (so a passing test cannot mean the
/// endpoint is simply broken), and organisation A probing B's ids through A's own route sees nothing.
/// </summary>
public sealed class QuerySurfaceTenancyTests : IntegrationTestBase
{
    public QuerySurfaceTenancyTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Org_A_cannot_read_org_B_game_statistics_through_its_own_route()
    {
        var b = await FinalizedGameAsync();
        var (tokenA, orgA) = await NewOrgWithOwnerAsync();
        var a = AuthenticatedClient(tokenA);

        // ── B sees its own game, so these endpoints genuinely return data ────────
        var ownShots = await ReadJson(await b.Client.GetAsync($"{b.GameUrl}/shot-chart"));
        ownShots.GetProperty("shots").GetArrayLength()
            .Should().BeGreaterThan(0, "B recorded a made field goal with coordinates");

        var ownLineups = await ReadJson(await b.Client.GetAsync($"{b.GameUrl}/lineups"));
        ownLineups.GetArrayLength().Should().BeGreaterThan(0, "a finalised game has lineup stints");

        // ── A probes B's game id through A's route: the tenant filter hides it ───
        var aGameUrl = $"/api/v1/organisations/{orgA}/games/{b.GameId}";

        var shots = await ReadJson(await a.GetAsync($"{aGameUrl}/shot-chart"));
        shots.GetProperty("shots").GetArrayLength()
            .Should().Be(0, "a shot chart must not cross the tenant boundary");

        var lineups = await ReadJson(await a.GetAsync($"{aGameUrl}/lineups"));
        lineups.GetArrayLength()
            .Should().Be(0, "lineups carry player ids and plus/minus for another org's game");

        var plays = await ReadJson(await a.GetAsync($"{aGameUrl}/play-by-play"));
        plays.GetArrayLength().Should().Be(0);

        (await a.GetAsync($"{aGameUrl}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var standings = await ReadJson(await a.GetAsync(
            $"/api/v1/organisations/{orgA}/competitions/{b.CompetitionId}/standings"));
        standings.GetArrayLength().Should().Be(0);

        var leaders = await ReadJson(await a.GetAsync(
            $"/api/v1/organisations/{orgA}/competitions/{b.CompetitionId}/leaders?stat=points"));
        leaders.GetProperty("rows").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Org_A_cannot_recompute_org_B_statistics()
    {
        var b = await FinalizedGameAsync();
        var (tokenA, orgA) = await NewOrgWithOwnerAsync();
        var a = AuthenticatedClient(tokenA);

        // ── B can recompute its own competition ──────────────────────────────────
        var own = await b.Client.PostAsync(
            $"/api/v1/organisations/{b.OrgId}/admin/recompute/competitions/{b.CompetitionId}", null);
        own.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(own)).GetProperty("statlineRowsWritten").GetInt32()
            .Should().BeGreaterThan(0, "B's own recompute must still rebuild its statistics");

        // ── A cannot, through its own route ──────────────────────────────────────
        var foreignGame = await a.PostAsync(
            $"/api/v1/organisations/{orgA}/admin/recompute/games/{b.GameId}", null);
        foreignGame.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "another organisation's game is absent, not merely forbidden");
        (await ReadJson(foreignGame)).GetProperty("code").GetString().Should().Be("GAME_NOT_FOUND");

        var foreignCompetition = await a.PostAsync(
            $"/api/v1/organisations/{orgA}/admin/recompute/competitions/{b.CompetitionId}", null);
        foreignCompetition.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadJson(foreignCompetition)).GetProperty("code").GetString()
            .Should().Be("COMPETITION_NOT_FOUND");

        // ── And B's statistics are untouched by the attempt ──────────────────────
        var stillThere = await ReadJson(await b.Client.GetAsync(
            $"/api/v1/organisations/{b.OrgId}/competitions/{b.CompetitionId}/standings"));
        stillThere.GetArrayLength().Should().Be(2, "both teams still have a standings row");
    }

    [Fact]
    public async Task A_players_career_shot_chart_still_spans_organisations()
    {
        // The deliberate ADR-003 exception: a career follows the PLAYER across tenants. Tightening the
        // per-game chart above must not quietly tenant-scope this one too.
        var b = await FinalizedGameAsync();
        var (tokenA, orgA) = await NewOrgWithOwnerAsync();
        var a = AuthenticatedClient(tokenA);

        var response = await a.GetAsync($"/api/v1/organisations/{orgA}/players/{b.ScorerPlayerId}/shot-chart");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var career = await ReadJson(response);

        career.GetProperty("shots").GetArrayLength()
            .Should().BeGreaterThan(0, "a career shot chart crosses organisations by design (ADR-003)");
    }

    // ── fixture ──────────────────────────────────────────────────────────────

    private sealed record Fixture(
        HttpClient Client, Guid OrgId, string CompetitionId, string GameId, string ScorerPlayerId)
    {
        public string GameUrl => $"/api/v1/organisations/{OrgId}/games/{GameId}";
    }

    /// <summary>An organisation with one complete, finalised game carrying a located made shot.</summary>
    private async Task<Fixture> FinalizedGameAsync()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var org = $"/api/v1/organisations/{orgId}";

        var seasonId = await PostIdAsync(client, $"{org}/seasons",
            new { name = "2025/26", startsOn = "2025-10-01", endsOn = "2026-06-30" });
        var competitionId = await PostIdAsync(client, $"{org}/competitions",
            new { seasonId, name = "League", slug = UniqueSlug(), format = "League", timezone = "Africa/Lagos" });

        var teamIds = new List<string>();
        var rosterEntries = new List<List<string>>();
        for (var t = 0; t < 2; t++)
        {
            var teamId = await PostIdAsync(client, $"{org}/teams", new { name = $"Team {t}", shortName = $"T{t}" });
            var competitionTeamId = await PostIdAsync(client,
                $"{org}/competitions/{competitionId}/teams", new { teamId });
            teamIds.Add(competitionTeamId);

            var entries = new List<string>();
            for (var p = 0; p < 5; p++)
            {
                var playerId = (await ReadJson(await client.PostAsJsonAsync($"{org}/registry/players",
                    new { firstName = $"P{p}", lastName = $"Team{t}", dateOfBirth = "2000-01-01", gender = "Male" })))
                    .GetProperty("playerId").GetString()!;
                entries.Add(await PostIdAsync(client, $"{org}/competition-teams/{competitionTeamId}/roster",
                    new { playerId, jerseyNumber = $"{(t * 10) + p + 1}" }));
            }

            rosterEntries.Add(entries);
        }

        var gameId = await PostIdAsync(client, $"{org}/competitions/{competitionId}/games",
            new
            {
                homeCompetitionTeamId = teamIds[0],
                awayCompetitionTeamId = teamIds[1],
                scheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            });

        var selections = rosterEntries
            .SelectMany(team => team.Select(id => new { rosterEntryId = id, isStarter = true })).ToList();
        (await client.PostAsJsonAsync($"{org}/games/{gameId}/lock-roster", new { selections }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"{org}/games/{gameId}/start", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshot = await ReadJson(await client.GetAsync($"{org}/games/{gameId}/roster"));
        var home = snapshot.EnumerateArray()
            .Where(e => e.GetProperty("competitionTeamId").GetString() == teamIds[0]).ToList();
        var scorerRosterId = home[0].GetProperty("id").GetString()!;
        var scorerPlayerId = home[0].GetProperty("playerId").GetString()!;

        // A short but complete period, driven as a scorer's table would: the clock runs down
        // monotonically, so the clock rules pass and stints accumulate minutes.
        var eventsUrl = $"{org}/games/{gameId}/events";
        await PostEventAsync(client, eventsUrl, new
        {
            eventId = Guid.CreateVersion7(), eventType = "PERIOD_START", eventSubtype = "Regulation",
            period = 1, gameClockMs = 600_000,
        });
        await PostEventAsync(client, eventsUrl, new
        {
            eventId = Guid.CreateVersion7(), eventType = "CLOCK_START", period = 1, gameClockMs = 600_000,
        });
        await PostEventAsync(client, eventsUrl, new
        {
            eventId = Guid.CreateVersion7(), eventType = "FIELD_GOAL_MADE", eventSubtype = "TwoPoint",
            period = 1, gameClockMs = 500_000, competitionTeamId = teamIds[0],
            gameRosterEntryId = scorerRosterId, shotXCm = 1150, shotYCm = 0,
        });
        await PostEventAsync(client, eventsUrl, new
        {
            eventId = Guid.CreateVersion7(), eventType = "CLOCK_STOP", period = 1, gameClockMs = 0,
        });
        await PostEventAsync(client, eventsUrl, new
        {
            eventId = Guid.CreateVersion7(), eventType = "PERIOD_END", period = 1, gameClockMs = 0,
        });

        (await client.PostAsync($"{org}/games/{gameId}/end", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"{org}/games/{gameId}/finalize", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Statistics land through the outbox, so drain before asserting on them.
        await Factory.Services.GetRequiredService<Hoops.Api.BackgroundServices.OutboxDrainer>()
            .DrainOnceAsync(CancellationToken.None);

        return new Fixture(client, orgId, competitionId, gameId, scorerPlayerId);
    }

    private static async Task PostEventAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }

    private static async Task<string> PostIdAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "POST {0}: {1}", url, await response.Content.ReadAsStringAsync());
        return (await ReadJson(response)).GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
