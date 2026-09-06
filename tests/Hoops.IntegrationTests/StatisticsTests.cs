using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.IntegrationTests;

/// <summary>
/// Phase 5's load-bearing guarantees: finalisation writes statlines that match the projection, and the
/// whole set of derived tables can be thrown away and rebuilt from the event log alone.
/// </summary>
public sealed class StatisticsTests : IntegrationTestBase
{
    public StatisticsTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Truncating_every_projection_table_and_recomputing_returns_identical_results()
    {
        var g = await FinalizedGameAsync();

        // Capture what the API serves from the persisted tables.
        var boxBefore = await Text(g.Client.GetAsync($"{g.GameUrl}/box-score"));
        var standingsBefore = await Text(g.Client.GetAsync($"{g.CompetitionUrl}/standings"));
        var playerStatsBefore = await Text(g.Client.GetAsync($"{g.CompetitionUrl}/player-stats"));
        var careerBefore = await Text(g.Client.GetAsync($"/api/v1/organisations/{g.OrgId}/players/{g.APlayerId}/career"));

        standingsBefore.Should().NotBe("[]", "the game was finalised, so standings exist");

        // THE test: destroy every derived table. The event log is untouched.
        await WithDbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE player_game_statlines, team_game_statlines, game_period_states, lineup_stints, "
                + "competition_player_aggregates, player_career_aggregates, competition_standings CASCADE;");
        });

        // Everything derived is now gone.
        var wiped = await Text(g.Client.GetAsync($"{g.CompetitionUrl}/standings"));
        wiped.Should().Be("[]", "the standings table was truncated");

        // Rebuild from the log alone.
        var recompute = await g.Client.PostAsync($"{g.AdminUrl}/recompute/competitions/{g.CompetitionId}", null);
        recompute.StatusCode.Should().Be(HttpStatusCode.OK);

        // Byte-for-byte identical to what the API served before.
        (await Text(g.Client.GetAsync($"{g.GameUrl}/box-score"))).Should().Be(boxBefore);
        (await Text(g.Client.GetAsync($"{g.CompetitionUrl}/standings"))).Should().Be(standingsBefore);
        (await Text(g.Client.GetAsync($"{g.CompetitionUrl}/player-stats"))).Should().Be(playerStatsBefore);
        (await Text(g.Client.GetAsync($"/api/v1/organisations/{g.OrgId}/players/{g.APlayerId}/career")))
            .Should().Be(careerBefore);
    }

    [Fact]
    public async Task Finalising_writes_statlines_that_match_the_projection()
    {
        var g = await FinalizedGameAsync();

        var box = await ReadJson(g.Client.GetAsync($"{g.GameUrl}/box-score"));
        var projectedTotal = box.GetProperty("players").EnumerateArray().Sum(p => p.GetProperty("points").GetInt32());

        var gameId = GameId.FromGuid(Guid.Parse(g.GameId));
        await WithDbAsync(async db =>
        {
            var lines = await db.PlayerGameStatlines.IgnoreQueryFilters()
                .Where(l => l.GameId == gameId).ToListAsync();
            lines.Should().NotBeEmpty("finalisation persists statlines");
            lines.Sum(l => l.Points).Should().Be(projectedTotal, "persisted statlines match the projection");

            var teams = await db.TeamGameStatlines.IgnoreQueryFilters()
                .Where(t => t.GameId == gameId).ToListAsync();
            teams.Should().HaveCount(2);

            // Team points equal the sum of that team's players' points.
            foreach (var team in teams)
            {
                lines.Where(l => l.CompetitionTeamId == team.CompetitionTeamId).Sum(l => l.Points)
                    .Should().Be(team.Points);
            }

            // Plus/minus across both teams nets to zero.
            lines.Sum(l => l.PlusMinus).Should().Be(0);
        });
    }

    [Fact]
    public async Task Reopening_a_finalised_game_withdraws_its_statistics_until_it_is_final_again()
    {
        var g = await FinalizedGameAsync();

        var gameId = GameId.FromGuid(Guid.Parse(g.GameId));
        await WithDbAsync(async db =>
            (await db.PlayerGameStatlines.IgnoreQueryFilters().CountAsync(l => l.GameId == gameId))
                .Should().BeGreaterThan(0));

        var reopen = await g.Client.PostAsJsonAsync($"{g.GameUrl}/reopen", new { reason = "misattributed rebound" });
        reopen.StatusCode.Should().Be(HttpStatusCode.OK);

        await DrainOutboxAsync();

        // A game under amendment must not keep contributing to leaderboards.
        var competitionId = CompetitionId.FromGuid(Guid.Parse(g.CompetitionId));
        await WithDbAsync(async db =>
        {
            (await db.PlayerGameStatlines.IgnoreQueryFilters().CountAsync(l => l.GameId == gameId))
                .Should().Be(0, "a reopened game no longer counts");
            (await db.CompetitionStandings.IgnoreQueryFilters().CountAsync(s => s.CompetitionId == competitionId))
                .Should().Be(0, "its competition has no finalised games left");
        });
    }

    [Fact]
    public async Task Qualification_is_recomputed_across_the_whole_competition()
    {
        var g = await FinalizedGameAsync();
        await DrainOutboxAsync();

        await WithDbAsync(async db =>
        {
            var competitionId = CompetitionId.FromGuid(Guid.Parse(g.CompetitionId));
            var aggregates = await db.CompetitionPlayerAggregates.IgnoreQueryFilters()
                .Where(a => a.CompetitionId == competitionId).ToListAsync();
            aggregates.Should().NotBeEmpty();

            // One game played of one scheduled, at the default 0.75 fraction, clears the threshold.
            aggregates.Should().OnlyContain(a => a.GamesPlayed == 1);
            aggregates.Should().Contain(a => a.IsQualified, "every player met the games threshold");
        });
    }

    [Fact]
    public async Task Careers_are_written_and_span_the_players_finalised_games()
    {
        var g = await FinalizedGameAsync();
        await DrainOutboxAsync();

        var career = await ReadJson(g.Client.GetAsync($"/api/v1/organisations/{g.OrgId}/players/{g.APlayerId}/career"));

        career.GetProperty("gamesPlayed").GetInt32().Should().Be(1);
        career.GetProperty("competitionsPlayed").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Finalising_enqueues_an_outbox_message_in_the_same_transaction()
    {
        var g = await FinalizedGameAsync();

        await WithDbAsync(async db =>
        {
            var messages = await db.OutboxMessages.ToListAsync();
            messages.Where(m => m.MessageType == "GameFinalized")
                .Should().Contain(m => m.Payload.Contains(g.GameId), "the finalisation wrote its own message");
        });
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed record Fixture(
        HttpClient Client, Guid OrgId, string CompetitionId, string GameId, string APlayerId)
    {
        public string GameUrl => $"/api/v1/organisations/{OrgId}/games/{GameId}";

        public string CompetitionUrl => $"/api/v1/organisations/{OrgId}/competitions/{CompetitionId}";

        public string AdminUrl => $"/api/v1/organisations/{OrgId}/admin";
    }

    /// <summary>Drains the outbox synchronously rather than waiting on the background timer.</summary>
    private async Task DrainOutboxAsync()
    {
        var drainer = Factory.Services.GetRequiredService<Hoops.Api.BackgroundServices.OutboxDrainer>();
        await drainer.DrainOnceAsync(CancellationToken.None);
    }

    /// <summary>Builds a competition with one played, reviewed, and finalised game.</summary>
    private async Task<Fixture> FinalizedGameAsync()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);

        var seasonId = await PostId(client, $"/api/v1/organisations/{orgId}/seasons",
            new { name = "2025/26", startsOn = "2025-10-01", endsOn = "2026-06-30" });
        var competitionId = await PostId(client, $"/api/v1/organisations/{orgId}/competitions",
            new { seasonId, name = "League", slug = UniqueSlug(), format = "League", timezone = "Africa/Lagos" });

        var teamIds = new List<string>();
        var rosterEntries = new List<List<string>>();
        var playerIds = new List<string>();
        for (var t = 0; t < 2; t++)
        {
            var teamId = await PostId(client, $"/api/v1/organisations/{orgId}/teams",
                new { name = $"Team {t}", shortName = $"T{t}" });
            var competitionTeamId = await PostId(client,
                $"/api/v1/organisations/{orgId}/competitions/{competitionId}/teams", new { teamId });
            teamIds.Add(competitionTeamId);

            var entries = new List<string>();
            for (var p = 0; p < 5; p++)
            {
                var playerId = (await ReadJson(client.PostAsJsonAsync(
                    $"/api/v1/organisations/{orgId}/registry/players",
                    new { firstName = $"P{p}", lastName = $"T{t}", dateOfBirth = "2000-01-01", gender = "Male" })))
                    .GetProperty("playerId").GetString()!;
                playerIds.Add(playerId);
                entries.Add(await PostId(client,
                    $"/api/v1/organisations/{orgId}/competition-teams/{competitionTeamId}/roster",
                    new { playerId, jerseyNumber = $"{t * 10 + p + 1}" }));
            }

            rosterEntries.Add(entries);
        }

        var gameId = await PostId(client, $"/api/v1/organisations/{orgId}/competitions/{competitionId}/games",
            new
            {
                homeCompetitionTeamId = teamIds[0],
                awayCompetitionTeamId = teamIds[1],
                scheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            });

        var selections = rosterEntries.SelectMany(t => t.Select(id => new { rosterEntryId = id, isStarter = true })).ToList();
        (await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/games/{gameId}/lock-roster", new { selections }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/v1/organisations/{orgId}/games/{gameId}/start", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Map to the frozen game-roster ids the events address.
        var snapshot = await ReadJson(client.GetAsync($"/api/v1/organisations/{orgId}/games/{gameId}/roster"));
        var home = snapshot.EnumerateArray().Where(e => e.GetProperty("competitionTeamId").GetString() == teamIds[0])
            .Select(e => e.GetProperty("id").GetString()!).ToList();
        var away = snapshot.EnumerateArray().Where(e => e.GetProperty("competitionTeamId").GetString() == teamIds[1])
            .Select(e => e.GetProperty("id").GetString()!).ToList();

        var eventsUrl = $"/api/v1/organisations/{orgId}/games/{gameId}/events";

        // A short but complete period, driven the way a scorer's table would: the clock runs down
        // monotonically, so the clock rules are satisfied and minutes accumulate.
        await Post(client, eventsUrl, Flow("PERIOD_START", "Regulation", 600_000));
        await Post(client, eventsUrl, Flow("CLOCK_START", null, 600_000));
        await Post(client, eventsUrl, Shot(home[0], teamIds[0], 500_000, secondary: home[1]));
        await Post(client, eventsUrl, Shot(home[2], teamIds[0], 400_000));
        await Post(client, eventsUrl, Shot(away[0], teamIds[1], 300_000));
        await Post(client, eventsUrl, Flow("CLOCK_STOP", null, 0));
        await Post(client, eventsUrl, Flow("PERIOD_END", null, 0));

        (await client.PostAsync($"/api/v1/organisations/{orgId}/games/{gameId}/end", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/v1/organisations/{orgId}/games/{gameId}/finalize", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Statistics land through the outbox, so drain before asserting on them.
        await DrainOutboxAsync();

        var homePlayerId = snapshot.EnumerateArray()
            .First(e => e.GetProperty("id").GetString() == home[0]).GetProperty("playerId").GetString()!;

        return new Fixture(client, orgId, competitionId, gameId, homePlayerId);
    }

    private sealed record ShotBody(
        Guid eventId, string eventType, string eventSubtype, int period, int gameClockMs,
        string competitionTeamId, string gameRosterEntryId, string? secondaryRosterEntryId, int shotXCm, int shotYCm);

    private sealed record FlowBody(
        Guid eventId, string eventType, string? eventSubtype, int period, int gameClockMs);

    private static ShotBody Shot(string shooter, string teamId, int clockMs, string? secondary = null)
        => new(Guid.CreateVersion7(), "FIELD_GOAL_MADE", "TwoPoint", 1, clockMs, teamId, shooter, secondary, 1150, 0);

    private static FlowBody Flow(string type, string? subtype, int clockMs)
        => new(Guid.CreateVersion7(), type, subtype, 1, clockMs);

    private static async Task Post(HttpClient client, string url, object body)
        => (await client.PostAsJsonAsync(url, body)).StatusCode.Should().Be(HttpStatusCode.Created);

    private static async Task<string> PostId(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, "POST {0}: {1}", url, await response.Content.ReadAsStringAsync());
        return (await ReadJson(Task.FromResult(response))).GetProperty("id").GetString()!;
    }

    private static async Task<string> Text(Task<HttpResponseMessage> call)
    {
        var response = await call;
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<JsonElement> ReadJson(Task<HttpResponseMessage> call)
    {
        var response = await call;
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
