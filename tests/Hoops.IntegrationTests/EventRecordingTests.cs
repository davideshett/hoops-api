using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Hoops.IntegrationTests;

/// <summary>
/// End-to-end checks for the event write path: idempotency, conflict detection, validation,
/// void/undo, and the derived live state.
/// </summary>
public sealed class EventRecordingTests : IntegrationTestBase
{
    public EventRecordingTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Submitting_the_same_event_id_twice_produces_one_row_and_identical_responses()
    {
        var g = await StartedGameAsync();
        var eventId = Guid.CreateVersion7();
        var body = MakeShot(eventId, g.HomeStarters[0], g.HomeTeamId);

        var first = await g.Client.PostAsJsonAsync(g.EventsUrl, body);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await first.Content.ReadAsStringAsync();

        var second = await g.Client.PostAsJsonAsync(g.EventsUrl, body);
        second.StatusCode.Should().Be(HttpStatusCode.OK, "a replay is not a new creation");
        var secondBody = await second.Content.ReadAsStringAsync();

        secondBody.Should().Be(firstBody, "an idempotent replay returns the original result byte-for-byte");

        var events = await ReadJson(await g.Client.GetAsync(g.EventsUrl));
        events.EnumerateArray().Count(e => e.GetProperty("eventId").GetString() == eventId.ToString())
            .Should().Be(1, "only one row is written");
    }

    [Fact]
    public async Task A_stale_last_known_sequence_returns_409_with_the_missing_events()
    {
        var g = await StartedGameAsync();

        // Record two events so the log moves ahead of the client.
        await g.Client.PostAsJsonAsync(g.EventsUrl, MakeShot(Guid.CreateVersion7(), g.HomeStarters[0], g.HomeTeamId));
        await g.Client.PostAsJsonAsync(g.EventsUrl, MakeShot(Guid.CreateVersion7(), g.HomeStarters[1], g.HomeTeamId));

        var stale = MakeShot(Guid.CreateVersion7(), g.HomeStarters[2], g.HomeTeamId) with { lastKnownSequence = 1L };
        var response = await g.Client.PostAsJsonAsync(g.EventsUrl, stale);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await ReadJson(response);
        problem.GetProperty("code").GetString().Should().Be("STALE_SEQUENCE");
        problem.GetProperty("detail").GetString().Should().Contain("Missing sequences",
            "the client is told what it missed so it can self-heal");
    }

    [Fact]
    public async Task A_three_pointer_tapped_inside_the_arc_is_rejected_with_shot_zone_mismatch()
    {
        var g = await StartedGameAsync();

        // (1150, 0) is 92.5 cm from the hoop — unmistakably a two.
        var body = MakeShot(Guid.CreateVersion7(), g.HomeStarters[0], g.HomeTeamId, subtype: "ThreePoint", x: 1150, y: 0);
        var response = await g.Client.PostAsJsonAsync(g.EventsUrl, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJson(response)).GetProperty("code").GetString().Should().Be("SHOT_ZONE_MISMATCH");
    }

    [Fact]
    public async Task An_overridden_tier_two_violation_is_accepted_and_flagged()
    {
        var g = await StartedGameAsync();
        var body = MakeShot(Guid.CreateVersion7(), g.HomeStarters[0], g.HomeTeamId, subtype: "ThreePoint", x: 1150, y: 0);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{g.EventsUrl}?override=true")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Override-Reason", "Scorer confirmed the shot was behind the line.");

        var response = await g.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var accepted = await ReadJson(response);
        accepted.GetProperty("event").GetProperty("wasOverridden").GetBoolean().Should().BeTrue();
        accepted.GetProperty("event").GetProperty("payload").GetProperty("overridden").GetString()
            .Should().Be("SHOT_ZONE_MISMATCH");
    }

    [Fact]
    public async Task Voiding_an_event_removes_its_contribution_and_leaves_the_row_in_place()
    {
        var g = await StartedGameAsync();
        var eventId = Guid.CreateVersion7();
        await g.Client.PostAsJsonAsync(g.EventsUrl, MakeShot(eventId, g.HomeStarters[0], g.HomeTeamId));

        var before = await ReadJson(await g.Client.GetAsync($"{g.GameUrl}/state"));
        before.GetProperty("score").GetProperty(g.HomeTeamId).GetInt32().Should().Be(2);

        var voided = await g.Client.PostAsJsonAsync($"{g.EventsUrl}/{eventId}/void", new { reason = "wrong player" });
        voided.StatusCode.Should().Be(HttpStatusCode.Created);

        var after = await ReadJson(await g.Client.GetAsync($"{g.GameUrl}/state"));
        after.GetProperty("score").GetProperty(g.HomeTeamId).GetInt32().Should().Be(0, "the contribution is gone");

        var events = await ReadJson(await g.Client.GetAsync(g.EventsUrl));
        var row = events.EnumerateArray().Single(e => e.GetProperty("eventId").GetString() == eventId.ToString());
        row.GetProperty("isVoided").GetBoolean().Should().BeTrue("the row remains, flagged");
    }

    [Fact]
    public async Task Undo_last_after_a_void_skips_the_already_voided_event()
    {
        var g = await StartedGameAsync();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        await g.Client.PostAsJsonAsync(g.EventsUrl, MakeShot(first, g.HomeStarters[0], g.HomeTeamId));
        await g.Client.PostAsJsonAsync(g.EventsUrl, MakeShot(second, g.HomeStarters[1], g.HomeTeamId));

        // Void the newest explicitly, then undo-last must target the older one, not the voided one.
        await g.Client.PostAsJsonAsync($"{g.EventsUrl}/{second}/void", new { reason = "mistake" });
        var undo = await g.Client.PostAsync($"{g.EventsUrl}/undo-last", null);
        undo.StatusCode.Should().Be(HttpStatusCode.Created);

        var events = await ReadJson(await g.Client.GetAsync(g.EventsUrl));
        Voided(events, first).Should().BeTrue("undo-last skipped the already-voided event and took the next one");
        Voided(events, second).Should().BeTrue();

        var state = await ReadJson(await g.Client.GetAsync($"{g.GameUrl}/state"));
        state.GetProperty("score").GetProperty(g.HomeTeamId).GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task The_box_score_is_derived_from_the_log()
    {
        var g = await StartedGameAsync();
        await g.Client.PostAsJsonAsync(g.EventsUrl,
            MakeShot(Guid.CreateVersion7(), g.HomeStarters[0], g.HomeTeamId, secondary: g.HomeStarters[1]));
        await g.Client.PostAsJsonAsync(g.EventsUrl,
            MakeShot(Guid.CreateVersion7(), g.HomeStarters[2], g.HomeTeamId, subtype: "ThreePoint", x: 500, y: 0));

        var box = await ReadJson(await g.Client.GetAsync($"{g.GameUrl}/box-score"));

        var scorer = box.GetProperty("players").EnumerateArray()
            .Single(p => p.GetProperty("gameRosterEntryId").GetString() == g.HomeStarters[0]);
        scorer.GetProperty("points").GetInt32().Should().Be(2);

        var assister = box.GetProperty("players").EnumerateArray()
            .Single(p => p.GetProperty("gameRosterEntryId").GetString() == g.HomeStarters[1]);
        assister.GetProperty("assists").GetInt32().Should().Be(1);
        assister.GetProperty("fieldGoalPercentage").ValueKind.Should().Be(JsonValueKind.Null,
            "percentages are null, not zero, when there were no attempts");

        var homeTeam = box.GetProperty("teams").EnumerateArray()
            .Single(t => t.GetProperty("competitionTeamId").GetString() == g.HomeTeamId);
        homeTeam.GetProperty("points").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task Events_cannot_be_recorded_before_the_game_starts()
    {
        var g = await LockedGameAsync();

        var response = await g.Client.PostAsJsonAsync(g.EventsUrl,
            MakeShot(Guid.CreateVersion7(), g.HomeStarters[0], g.HomeTeamId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJson(response)).GetProperty("code").GetString().Should().Be("GAME_NOT_IN_PROGRESS");
    }

    [Fact]
    public async Task Resync_returns_only_events_after_the_given_sequence()
    {
        var g = await StartedGameAsync();
        await g.Client.PostAsJsonAsync(g.EventsUrl, MakeShot(Guid.CreateVersion7(), g.HomeStarters[0], g.HomeTeamId));
        await g.Client.PostAsJsonAsync(g.EventsUrl, MakeShot(Guid.CreateVersion7(), g.HomeStarters[1], g.HomeTeamId));

        var all = await ReadJson(await g.Client.GetAsync(g.EventsUrl));
        var head = all.EnumerateArray().Max(e => e.GetProperty("sequence").GetInt64());

        var since = await ReadJson(await g.Client.GetAsync($"{g.EventsUrl}?sinceSequence={head - 1}"));
        since.GetArrayLength().Should().Be(1);
        since[0].GetProperty("sequence").GetInt64().Should().Be(head);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed record GameFixture(
        HttpClient Client, Guid OrgId, string GameId, string HomeTeamId, IReadOnlyList<string> HomeStarters)
    {
        public string GameUrl => $"/api/v1/organisations/{OrgId}/games/{GameId}";

        public string EventsUrl => $"{GameUrl}/events";
    }

    private static bool Voided(JsonElement events, Guid id)
        => events.EnumerateArray().Single(e => e.GetProperty("eventId").GetString() == id.ToString())
            .GetProperty("isVoided").GetBoolean();

    /// <summary>The submission body, as a concrete type so <c>with</c> and JSON extensions both work.</summary>
    private sealed record ShotBody(
        Guid eventId, long? lastKnownSequence, string eventType, string eventSubtype, int period,
        int gameClockMs, string competitionTeamId, string gameRosterEntryId, string? secondaryRosterEntryId,
        int shotXCm, int shotYCm);

    /// <summary>A shot payload; defaults to a clean two from the restricted area.</summary>
    private static ShotBody MakeShot(
        Guid eventId, string shooter, string teamId, string subtype = "TwoPoint",
        int x = 1150, int y = 0, string? secondary = null)
        => new(eventId, null, "FIELD_GOAL_MADE", subtype, 1, 500000, teamId, shooter, secondary, x, y);

    private async Task<GameFixture> StartedGameAsync()
    {
        var g = await LockedGameAsync();
        var start = await g.Client.PostAsync($"{g.GameUrl}/start", null);
        start.StatusCode.Should().Be(HttpStatusCode.OK, await start.Content.ReadAsStringAsync());
        return g;
    }

    private async Task<GameFixture> LockedGameAsync()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);

        var seasonId = await PostIdAsync(client, $"/api/v1/organisations/{orgId}/seasons",
            new { name = "2025/26", startsOn = "2025-10-01", endsOn = "2026-06-30" });
        var competitionId = await PostIdAsync(client, $"/api/v1/organisations/{orgId}/competitions",
            new { seasonId, name = "League", slug = UniqueSlug(), format = "League", timezone = "Africa/Lagos" });

        var teamIds = new List<string>();
        var entries = new List<List<string>>();
        for (var t = 0; t < 2; t++)
        {
            var teamId = await PostIdAsync(client, $"/api/v1/organisations/{orgId}/teams",
                new { name = $"Team {t}", shortName = $"T{t}" });
            var competitionTeamId = await PostIdAsync(client,
                $"/api/v1/organisations/{orgId}/competitions/{competitionId}/teams", new { teamId });
            teamIds.Add(competitionTeamId);

            var teamEntries = new List<string>();
            for (var p = 0; p < 5; p++)
            {
                var playerId = (await ReadJson(await client.PostAsJsonAsync(
                    $"/api/v1/organisations/{orgId}/registry/players",
                    new { firstName = $"P{p}", lastName = $"T{t}", dateOfBirth = "2000-01-01", gender = "Male" })))
                    .GetProperty("playerId").GetString();
                teamEntries.Add(await PostIdAsync(client,
                    $"/api/v1/organisations/{orgId}/competition-teams/{competitionTeamId}/roster",
                    new { playerId, jerseyNumber = $"{t * 10 + p + 1}" }));
            }

            entries.Add(teamEntries);
        }

        var gameId = await PostIdAsync(client, $"/api/v1/organisations/{orgId}/competitions/{competitionId}/games",
            new
            {
                homeCompetitionTeamId = teamIds[0],
                awayCompetitionTeamId = teamIds[1],
                scheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            });

        // All ten are starters, which satisfies playersOnCourt = 5 per team.
        var selections = entries.SelectMany(team => team.Select(id => new { rosterEntryId = id, isStarter = true })).ToList();
        var locked = await client.PostAsJsonAsync(
            $"/api/v1/organisations/{orgId}/games/{gameId}/lock-roster", new { selections });
        locked.StatusCode.Should().Be(HttpStatusCode.OK, await locked.Content.ReadAsStringAsync());

        // Map roster entries to the frozen game-roster ids the events must address.
        var snapshot = await ReadJson(await client.GetAsync($"/api/v1/organisations/{orgId}/games/{gameId}/roster"));
        var homeStarters = snapshot.EnumerateArray()
            .Where(e => e.GetProperty("competitionTeamId").GetString() == teamIds[0])
            .Select(e => e.GetProperty("id").GetString()!)
            .ToList();

        return new GameFixture(client, orgId, gameId, teamIds[0], homeStarters);
    }

    private static async Task<string> PostIdAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, "POST {0}: {1}", url, await response.Content.ReadAsStringAsync());
        return (await ReadJson(response)).GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
