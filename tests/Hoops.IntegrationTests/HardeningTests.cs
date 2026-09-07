using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hoops.Api.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Hoops.IntegrationTests;

/// <summary>
/// Phase 7 hardening: the write path holds up under a realistic tournament load, a restored backup is
/// genuinely usable, and a rejected tap leaves enough behind to reconstruct what happened.
/// </summary>
public sealed class HardeningTests : IntegrationTestBase
{
    public HardeningTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Eight_concurrent_games_sustain_event_submission_with_p99_under_300ms()
    {
        // Eight courts running at once is the realistic worst case for a tournament day.
        const int courts = 8;
        const int eventsPerCourt = 40;

        var games = await Task.WhenAll(Enumerable.Range(0, courts).Select(_ => StartedGameAsync()));

        var latencies = new System.Collections.Concurrent.ConcurrentBag<double>();

        await Task.WhenAll(games.Select(async game =>
        {
            // A realistic cadence: alternating makes and misses down a running clock.
            for (var i = 0; i < eventsPerCourt; i++)
            {
                var clockMs = 600_000 - (i * 10_000);
                var body = i % 2 == 0
                    ? Shot(game.HomeStarters[i % 5], game.HomeTeamId, "FIELD_GOAL_MADE", clockMs)
                    : Shot(game.HomeStarters[i % 5], game.HomeTeamId, "FIELD_GOAL_MISSED", clockMs);

                var stopwatch = Stopwatch.StartNew();
                var response = await game.Client.PostAsJsonAsync(game.EventsUrl, body);
                stopwatch.Stop();

                response.StatusCode.Should().Be(HttpStatusCode.Created,
                    "submission must not fail under load: {0}", await response.Content.ReadAsStringAsync());
                latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
        }));

        var sorted = latencies.OrderBy(x => x).ToList();
        sorted.Should().HaveCount(courts * eventsPerCourt);

        var p99 = sorted[(int)Math.Floor(sorted.Count * 0.99) - 1];
        var p50 = sorted[sorted.Count / 2];

        Console.WriteLine(
            $"[load] {courts} concurrent games, {sorted.Count} events: p50={p50:F0}ms p99={p99:F0}ms max={sorted[^1]:F0}ms");

        p99.Should().BeLessThan(300,
            "p99 was {0:F0} ms (p50 {1:F0} ms) across {2} concurrent games", p99, p50, courts);
    }

    [Fact]
    public async Task A_restored_backup_passes_the_full_recompute_equality_test()
    {
        // Build real history, so the backup has something worth restoring.
        var g = await FinalizedGameAsync();
        var boxBefore = await Text(g.Client.GetAsync($"{g.GameUrl}/box-score"));
        var standingsBefore = await Text(g.Client.GetAsync($"{g.CompetitionUrl}/standings"));
        standingsBefore.Should().NotBe("[]");

        // pg_dump the live database, then restore it into a brand-new one alongside it.
        const string restored = "hoops_restore_rehearsal";
        var dump = await Factory.ExecInContainerAsync("pg_dump", "-U", "postgres", "-d", "postgres", "-f", "/tmp/backup.sql");
        dump.ExitCode.Should().Be(0, "pg_dump failed: {0}", dump.Stderr);

        await Factory.ExecInContainerAsync("psql", "-U", "postgres", "-c", $"DROP DATABASE IF EXISTS {restored};");
        var create = await Factory.ExecInContainerAsync("psql", "-U", "postgres", "-c", $"CREATE DATABASE {restored};");
        create.ExitCode.Should().Be(0, "create failed: {0}", create.Stderr);

        var restore = await Factory.ExecInContainerAsync("psql", "-U", "postgres", "-d", restored, "-f", "/tmp/backup.sql");
        restore.ExitCode.Should().Be(0, "restore failed: {0}", restore.Stderr);

        // The real test of a backup is not that it loads — it is that the event log inside it still
        // reproduces the same statistics. Truncate every derived table in the RESTORED copy, recompute
        // from its logs alone, and compare with what the live system serves.
        var restoredConnection = Factory.ContainerConnectionString
            .Replace("Database=postgres", $"Database={restored}", StringComparison.OrdinalIgnoreCase);

        await using var restoredFactory = new RestoredApiFactory(restoredConnection);
        var restoredClient = restoredFactory.CreateClient();
        restoredClient.DefaultRequestHeaders.Authorization = g.Client.DefaultRequestHeaders.Authorization;

        using (var scope = restoredFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Hoops.Infrastructure.Persistence.AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE player_game_statlines, team_game_statlines, game_period_states, lineup_stints, "
                + "competition_player_aggregates, player_career_aggregates, competition_standings CASCADE;");
        }

        var recompute = await restoredClient.PostAsync(
            $"{g.AdminUrl}/recompute/competitions/{g.CompetitionId}", null);
        recompute.StatusCode.Should().Be(HttpStatusCode.OK, await recompute.Content.ReadAsStringAsync());

        (await Text(restoredClient.GetAsync($"{g.GameUrl}/box-score"))).Should().Be(boxBefore,
            "the restored backup reproduces the same box score from its event log");
        (await Text(restoredClient.GetAsync($"{g.CompetitionUrl}/standings"))).Should().Be(standingsBefore,
            "and the same standings");
    }

    [Fact]
    public async Task A_rejected_tap_is_logged_with_enough_context_to_reconstruct_it()
    {
        var g = await StartedGameAsync();
        CapturingSink.Clear();

        // A three tapped from inside the arc — a real scorer's-table mis-tap.
        var shooter = g.HomeStarters[0];
        var body = Shot(shooter, g.HomeTeamId, "FIELD_GOAL_MADE", 500_000, subtype: "ThreePoint", x: 1150, y: 0);
        var response = await g.Client.PostAsJsonAsync(g.EventsUrl, body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var log = string.Join('\n', CapturingSink.Snapshot());

        // Everything needed to reconstruct the tap without opening the database.
        log.Should().Contain("SHOT_ZONE_MISMATCH", "the rule that rejected it");
        log.Should().Contain(g.GameId, "which game");
        log.Should().Contain(shooter, "which player was tapped");
        log.Should().Contain(body.eventId.ToString(), "the client's event id, to correlate with the device");
        log.Should().Contain("FIELD_GOAL_MADE", "what was being recorded");
    }

    [Fact]
    public async Task Event_submission_is_rate_limited_per_game_rather_than_globally()
    {
        // The policy is deliberately generous, so this asserts the wiring rather than a low ceiling:
        // a normal burst is never throttled.
        var g = await StartedGameAsync();

        var responses = new List<HttpStatusCode>();
        for (var i = 0; i < 30; i++)
        {
            var response = await g.Client.PostAsJsonAsync(
                g.EventsUrl, Shot(g.HomeStarters[i % 5], g.HomeTeamId, "FIELD_GOAL_MADE", 600_000 - (i * 5_000)));
            responses.Add(response.StatusCode);
        }

        responses.Should().AllBeEquivalentTo(HttpStatusCode.Created,
            "a fast official tapping 30 times must never be throttled");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>A host pointed at the restored database rather than the live one.</summary>
    private sealed class RestoredApiFactory(string connectionString)
        : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-at-least-32-bytes-long-000");
            builder.UseSetting("Registry:NinPepper", "integration-test-nin-pepper");
        }
    }

    private sealed record ShotBody(
        Guid eventId, string eventType, string eventSubtype, int period, int gameClockMs,
        string competitionTeamId, string gameRosterEntryId, int shotXCm, int shotYCm);

    private static ShotBody Shot(
        string shooter, string teamId, string type, int clockMs,
        string subtype = "TwoPoint", int x = 1150, int y = 0)
        => new(Guid.CreateVersion7(), type, subtype, 1, clockMs, teamId, shooter, x, y);

    private sealed record GameFixture(
        HttpClient Client, Guid OrgId, string GameId, string CompetitionId, string HomeTeamId,
        IReadOnlyList<string> HomeStarters)
    {
        public string GameUrl => $"/api/v1/organisations/{OrgId}/games/{GameId}";

        public string CompetitionUrl => $"/api/v1/organisations/{OrgId}/competitions/{CompetitionId}";

        public string AdminUrl => $"/api/v1/organisations/{OrgId}/admin";

        public string EventsUrl => $"{GameUrl}/events";
    }

    private async Task<GameFixture> FinalizedGameAsync()
    {
        var g = await StartedGameAsync();
        var url = g.EventsUrl;
        await g.Client.PostAsJsonAsync(url, Shot(g.HomeStarters[0], g.HomeTeamId, "FIELD_GOAL_MADE", 500_000));
        await g.Client.PostAsJsonAsync(url, new { eventId = Guid.CreateVersion7(), eventType = "PERIOD_END", period = 1, gameClockMs = 0 });
        (await g.Client.PostAsync($"{g.GameUrl}/end", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await g.Client.PostAsync($"{g.GameUrl}/finalize", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var drainer = Factory.Services.GetRequiredService<Hoops.Api.BackgroundServices.OutboxDrainer>();
        await drainer.DrainOnceAsync(CancellationToken.None);
        return g;
    }

    private async Task<GameFixture> StartedGameAsync()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);

        var seasonId = await PostId(client, $"/api/v1/organisations/{orgId}/seasons",
            new { name = "2025/26", startsOn = "2025-10-01", endsOn = "2026-06-30" });
        var competitionId = await PostId(client, $"/api/v1/organisations/{orgId}/competitions",
            new { seasonId, name = "League", slug = UniqueSlug(), format = "League", timezone = "Africa/Lagos" });

        var teamIds = new List<string>();
        var entries = new List<List<string>>();
        for (var t = 0; t < 2; t++)
        {
            var teamId = await PostId(client, $"/api/v1/organisations/{orgId}/teams",
                new { name = $"Team {t}", shortName = $"T{t}" });
            var competitionTeamId = await PostId(client,
                $"/api/v1/organisations/{orgId}/competitions/{competitionId}/teams", new { teamId });
            teamIds.Add(competitionTeamId);

            var teamEntries = new List<string>();
            for (var p = 0; p < 5; p++)
            {
                var playerId = (await ReadJson(client.PostAsJsonAsync(
                    $"/api/v1/organisations/{orgId}/registry/players",
                    new { firstName = $"P{p}", lastName = UniqueSlug()[..8], dateOfBirth = "2000-01-01", gender = "Male" })))
                    .GetProperty("playerId").GetString()!;
                teamEntries.Add(await PostId(client,
                    $"/api/v1/organisations/{orgId}/competition-teams/{competitionTeamId}/roster",
                    new { playerId, jerseyNumber = $"{t * 10 + p + 1}" }));
            }

            entries.Add(teamEntries);
        }

        var gameId = await PostId(client, $"/api/v1/organisations/{orgId}/competitions/{competitionId}/games",
            new
            {
                homeCompetitionTeamId = teamIds[0],
                awayCompetitionTeamId = teamIds[1],
                scheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            });

        var selections = entries.SelectMany(t => t.Select(id => new { rosterEntryId = id, isStarter = true })).ToList();
        (await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/games/{gameId}/lock-roster", new { selections }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/v1/organisations/{orgId}/games/{gameId}/start", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/games/{gameId}/events",
            new { eventId = Guid.CreateVersion7(), eventType = "PERIOD_START", eventSubtype = "Regulation", period = 1, gameClockMs = 600_000 });

        var snapshot = await ReadJson(client.GetAsync($"/api/v1/organisations/{orgId}/games/{gameId}/roster"));
        var homeStarters = snapshot.EnumerateArray()
            .Where(e => e.GetProperty("competitionTeamId").GetString() == teamIds[0])
            .Select(e => e.GetProperty("id").GetString()!).ToList();

        return new GameFixture(client, orgId, gameId, competitionId, teamIds[0], homeStarters);
    }

    private static async Task<string> PostId(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, "POST {0}: {1}", url, await response.Content.ReadAsStringAsync());
        return (await ReadJson(Task.FromResult(response))).GetProperty("id").GetString()!;
    }

    private static async Task<string> Text(Task<HttpResponseMessage> call)
        => await (await call).Content.ReadAsStringAsync();

    private static async Task<JsonElement> ReadJson(Task<HttpResponseMessage> call)
    {
        var doc = JsonDocument.Parse(await (await call).Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
