using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Hoops.IntegrationTests;

public sealed class FixtureGenerationTests : IntegrationTestBase
{
    public FixtureGenerationTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Round_robin_generation_for_eight_teams_produces_28_fixtures_each_pairing_once()
    {
        var ctx = await SetUpCompetitionWithTeamsAsync(8);

        var generated = await ctx.Client.PostAsJsonAsync(GenerateUrl(ctx, "round-robin"),
            new { doubleRound = false, firstGameAt = DateTimeOffset.UtcNow.AddDays(7) });
        generated.StatusCode.Should().Be(HttpStatusCode.OK);

        var games = (await ReadJson(generated)).EnumerateArray().ToList();
        games.Should().HaveCount(28);

        var pairings = games.Select(Unordered).ToList();
        pairings.Should().OnlyHaveUniqueItems("each pair meets exactly once");
        games.Should().OnlyContain(g =>
            g.GetProperty("homeCompetitionTeamId").GetString() != g.GetProperty("awayCompetitionTeamId").GetString());
    }

    [Fact]
    public async Task Double_round_robin_produces_56_with_home_and_away_alternated()
    {
        var ctx = await SetUpCompetitionWithTeamsAsync(8);

        var generated = await ctx.Client.PostAsJsonAsync(GenerateUrl(ctx, "round-robin"),
            new { doubleRound = true, firstGameAt = DateTimeOffset.UtcNow.AddDays(7) });
        generated.StatusCode.Should().Be(HttpStatusCode.OK);

        var games = (await ReadJson(generated)).EnumerateArray().ToList();
        games.Should().HaveCount(56);

        foreach (var group in games.GroupBy(Unordered))
        {
            group.Should().HaveCount(2);
            group.Select(g => (g.GetProperty("homeCompetitionTeamId").GetString(),
                    g.GetProperty("awayCompetitionTeamId").GetString()))
                .Distinct().Should().HaveCount(2, "the reverse leg swaps home and away");
        }
    }

    [Fact]
    public async Task Knockout_generation_for_eight_teams_produces_four_seeded_first_round_games()
    {
        var ctx = await SetUpCompetitionWithTeamsAsync(8, seeded: true);

        var generated = await ctx.Client.PostAsJsonAsync(GenerateUrl(ctx, "knockout"),
            new { firstGameAt = DateTimeOffset.UtcNow.AddDays(7) });
        generated.StatusCode.Should().Be(HttpStatusCode.OK);

        var games = (await ReadJson(generated)).EnumerateArray().ToList();
        games.Should().HaveCount(4);

        // Seed 1 (first entered team) must face seed 8 (last).
        var topSeed = ctx.CompetitionTeamIds[0];
        var bottomSeed = ctx.CompetitionTeamIds[7];
        games.Should().Contain(g =>
            g.GetProperty("homeCompetitionTeamId").GetString() == topSeed &&
            g.GetProperty("awayCompetitionTeamId").GetString() == bottomSeed);
    }

    [Fact]
    public async Task A_team_cannot_be_scheduled_against_itself()
    {
        var ctx = await SetUpCompetitionWithTeamsAsync(2);

        var response = await ctx.Client.PostAsJsonAsync(GamesUrl(ctx), new
        {
            homeCompetitionTeamId = ctx.CompetitionTeamIds[0],
            awayCompetitionTeamId = ctx.CompetitionTeamIds[0],
            scheduledAt = DateTimeOffset.UtcNow.AddDays(1),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Generation_requires_at_least_two_teams()
    {
        var ctx = await SetUpCompetitionWithTeamsAsync(1);

        var generated = await ctx.Client.PostAsJsonAsync(GenerateUrl(ctx, "round-robin"),
            new { doubleRound = false, firstGameAt = DateTimeOffset.UtcNow.AddDays(7) });

        generated.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJson(generated)).GetProperty("code").GetString().Should().Be("NOT_ENOUGH_TEAMS");
    }

    [Fact]
    public async Task A_scheduled_game_can_be_rescheduled_postponed_and_deleted()
    {
        var ctx = await SetUpCompetitionWithTeamsAsync(2);
        var gameId = await PostIdAsync(ctx.Client, GamesUrl(ctx), new
        {
            homeCompetitionTeamId = ctx.CompetitionTeamIds[0],
            awayCompetitionTeamId = ctx.CompetitionTeamIds[1],
            scheduledAt = DateTimeOffset.UtcNow.AddDays(1),
        });
        var gameUrl = $"/api/v1/organisations/{ctx.OrgId}/games/{gameId}";

        var newTime = DateTimeOffset.UtcNow.AddDays(5);
        var reschedule = await ctx.Client.PatchAsJsonAsync(gameUrl, new { scheduledAt = newTime });
        reschedule.StatusCode.Should().Be(HttpStatusCode.OK);

        var postpone = await ctx.Client.PostAsync($"{gameUrl}/postpone", null);
        postpone.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(postpone)).GetProperty("status").GetString().Should().Be("Postponed");

        // Rescheduling a postponed game returns it to Scheduled.
        var back = await ctx.Client.PatchAsJsonAsync(gameUrl, new { scheduledAt = newTime.AddDays(1) });
        (await ReadJson(back)).GetProperty("status").GetString().Should().Be("Scheduled");

        (await ctx.Client.DeleteAsync(gameUrl)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ctx.Client.GetAsync(gameUrl)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Officials_can_be_assigned_and_listed()
    {
        var ctx = await SetUpCompetitionWithTeamsAsync(2);
        var gameId = await PostIdAsync(ctx.Client, GamesUrl(ctx), new
        {
            homeCompetitionTeamId = ctx.CompetitionTeamIds[0],
            awayCompetitionTeamId = ctx.CompetitionTeamIds[1],
            scheduledAt = DateTimeOffset.UtcNow.AddDays(1),
        });

        var assign = await ctx.Client.PostAsJsonAsync(
            $"/api/v1/organisations/{ctx.OrgId}/games/{gameId}/officials",
            new { fullName = "Referee Ade", role = "Referee" });
        assign.StatusCode.Should().Be(HttpStatusCode.Created);

        var officials = await ctx.Client.GetAsync($"/api/v1/organisations/{ctx.OrgId}/games/{gameId}/officials");
        (await ReadJson(officials)).GetArrayLength().Should().Be(1);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed record CompetitionContext(HttpClient Client, Guid OrgId, string CompetitionId, IReadOnlyList<string> CompetitionTeamIds);

    private static string GamesUrl(CompetitionContext c)
        => $"/api/v1/organisations/{c.OrgId}/competitions/{c.CompetitionId}/games";

    private static string GenerateUrl(CompetitionContext c, string kind) => $"{GamesUrl(c)}/generate/{kind}";

    private static (string, string) Unordered(JsonElement game)
    {
        var home = game.GetProperty("homeCompetitionTeamId").GetString()!;
        var away = game.GetProperty("awayCompetitionTeamId").GetString()!;
        return string.CompareOrdinal(home, away) < 0 ? (home, away) : (away, home);
    }

    private async Task<CompetitionContext> SetUpCompetitionWithTeamsAsync(int teamCount, bool seeded = false)
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);

        var seasonId = await PostIdAsync(client, $"/api/v1/organisations/{orgId}/seasons",
            new { name = "2025/26", startsOn = "2025-10-01", endsOn = "2026-06-30" });
        var competitionId = await PostIdAsync(client, $"/api/v1/organisations/{orgId}/competitions",
            new { seasonId, name = "Cup", slug = UniqueSlug(), format = "Knockout", timezone = "Africa/Lagos" });

        var competitionTeamIds = new List<string>();
        for (var i = 0; i < teamCount; i++)
        {
            var teamId = await PostIdAsync(client, $"/api/v1/organisations/{orgId}/teams",
                new { name = $"Team {i}", shortName = $"T{i}" });
            var competitionTeamId = await PostIdAsync(client,
                $"/api/v1/organisations/{orgId}/competitions/{competitionId}/teams", new { teamId });
            competitionTeamIds.Add(competitionTeamId);

            if (seeded)
            {
                var seed = await client.PatchAsJsonAsync(
                    $"/api/v1/organisations/{orgId}/competition-teams/{competitionTeamId}", new { seed = i + 1 });
                seed.StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }

        return new CompetitionContext(client, orgId, competitionId, competitionTeamIds);
    }

    private static async Task<string> PostIdAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, "POST {0} should succeed: {1}", url,
            await response.Content.ReadAsStringAsync());
        return (await ReadJson(response)).GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
