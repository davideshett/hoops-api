using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Hoops.IntegrationTests;

/// <summary>
/// The point of Phase 3: once a game's roster is locked, neither a jersey change on the source roster
/// nor an edit to the competition's rule set may alter the frozen game snapshot.
/// </summary>
public sealed class RosterLockSnapshotTests : IntegrationTestBase
{
    public RosterLockSnapshotTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task After_lock_changing_a_jersey_number_does_not_change_the_game_roster()
    {
        var ctx = await SetUpLockedGameAsync();

        // The snapshot recorded jersey "7" for our player.
        var before = await GetGameRosterAsync(ctx);
        var snapshotEntry = before.EnumerateArray().First(e => e.GetProperty("playerId").GetString() == ctx.FirstPlayerId);
        snapshotEntry.GetProperty("jerseyNumber").GetString().Should().Be("7");

        // Change the jersey on the LIVE roster entry after the lock.
        var change = await ctx.Client.PatchAsJsonAsync(
            $"/api/v1/organisations/{ctx.OrgId}/roster-entries/{ctx.FirstRosterEntryId}", new { jerseyNumber = "23" });
        change.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(change)).GetProperty("jerseyNumber").GetString().Should().Be("23", "the live roster did change");

        // The frozen snapshot is untouched.
        var after = await GetGameRosterAsync(ctx);
        after.EnumerateArray().First(e => e.GetProperty("playerId").GetString() == ctx.FirstPlayerId)
            .GetProperty("jerseyNumber").GetString().Should().Be("7", "game_roster_entries is frozen at lock time");
    }

    [Fact]
    public async Task After_lock_editing_the_competition_rule_set_does_not_change_the_snapshot()
    {
        var ctx = await SetUpLockedGameAsync();

        // The game froze the competition's rules at lock time (default FIBA: 600s periods).
        var locked = await ctx.Client.GetAsync($"/api/v1/organisations/{ctx.OrgId}/games/{ctx.GameId}");
        (await ReadJson(locked)).GetProperty("ruleSetSnapshot").GetProperty("periodDurationSeconds").GetInt32()
            .Should().Be(600);

        // Change the competition's rule set after the lock.
        var edit = await ctx.Client.PatchAsJsonAsync(
            $"/api/v1/organisations/{ctx.OrgId}/competitions/{ctx.CompetitionId}",
            new { ruleSet = new { periodDurationSeconds = 480, playersOnCourt = 5, minRosterSize = 5 } });
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(edit)).GetProperty("ruleSet").GetProperty("periodDurationSeconds").GetInt32()
            .Should().Be(480, "the competition's live rules did change");

        // The game's frozen snapshot is untouched.
        var after = await ctx.Client.GetAsync($"/api/v1/organisations/{ctx.OrgId}/games/{ctx.GameId}");
        (await ReadJson(after)).GetProperty("ruleSetSnapshot").GetProperty("periodDurationSeconds").GetInt32()
            .Should().Be(600, "rule_set_snapshot is frozen at lock time");
    }

    [Fact]
    public async Task Roster_lock_fails_when_a_team_has_fewer_than_min_roster_size()
    {
        var ctx = await SetUpGameWithRostersAsync(playersPerTeam: 4); // below the FIBA minimum of 5

        var selections = ctx.AllRosterEntryIds.Select((id, i) => new { rosterEntryId = id, isStarter = i % 4 < 4 }).ToList();
        var lockResult = await ctx.Client.PostAsJsonAsync(
            $"/api/v1/organisations/{ctx.OrgId}/games/{ctx.GameId}/lock-roster", new { selections });

        lockResult.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJson(lockResult)).GetProperty("code").GetString().Should().Be("ROSTER_TOO_SMALL");
    }

    [Fact]
    public async Task Roster_lock_fails_unless_exactly_players_on_court_starters_are_marked()
    {
        var ctx = await SetUpGameWithRostersAsync(playersPerTeam: 6);

        // Mark only 4 starters per team (FIBA requires exactly 5).
        var selections = ctx.PerTeamRosterEntryIds
            .SelectMany(team => team.Select((id, i) => new { rosterEntryId = id, isStarter = i < 4 }))
            .ToList();

        var lockResult = await ctx.Client.PostAsJsonAsync(
            $"/api/v1/organisations/{ctx.OrgId}/games/{ctx.GameId}/lock-roster", new { selections });

        lockResult.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJson(lockResult)).GetProperty("code").GetString().Should().Be("WRONG_STARTER_COUNT");
    }

    [Fact]
    public async Task A_locked_game_cannot_be_rescheduled()
    {
        var ctx = await SetUpLockedGameAsync();

        var reschedule = await ctx.Client.PatchAsJsonAsync(
            $"/api/v1/organisations/{ctx.OrgId}/games/{ctx.GameId}",
            new { scheduledAt = DateTimeOffset.UtcNow.AddDays(3) });

        reschedule.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJson(reschedule)).GetProperty("code").GetString().Should().Be("GAME_NOT_RESCHEDULABLE");
    }

    // ── setup helpers ────────────────────────────────────────────────────────

    private sealed record GameContext(
        HttpClient Client, Guid OrgId, string CompetitionId, string GameId,
        IReadOnlyList<string> AllRosterEntryIds, IReadOnlyList<IReadOnlyList<string>> PerTeamRosterEntryIds,
        string FirstRosterEntryId, string FirstPlayerId);

    private async Task<GameContext> SetUpLockedGameAsync()
    {
        var ctx = await SetUpGameWithRostersAsync(playersPerTeam: 6);

        // Exactly 5 starters per team.
        var selections = ctx.PerTeamRosterEntryIds
            .SelectMany(team => team.Select((id, i) => new { rosterEntryId = id, isStarter = i < 5 }))
            .ToList();

        var lockResult = await ctx.Client.PostAsJsonAsync(
            $"/api/v1/organisations/{ctx.OrgId}/games/{ctx.GameId}/lock-roster", new { selections });
        lockResult.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(lockResult)).GetProperty("status").GetString().Should().Be("RosterLocked");

        return ctx;
    }

    private async Task<GameContext> SetUpGameWithRostersAsync(int playersPerTeam)
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);

        var seasonId = await PostIdAsync(client, $"/api/v1/organisations/{orgId}/seasons",
            new { name = "2025/26", startsOn = "2025-10-01", endsOn = "2026-06-30" });
        var competitionId = await PostIdAsync(client, $"/api/v1/organisations/{orgId}/competitions",
            new { seasonId, name = "League", slug = UniqueSlug(), format = "League", timezone = "Africa/Lagos" });

        var competitionTeamIds = new List<string>();
        var perTeam = new List<IReadOnlyList<string>>();
        string? firstRosterEntryId = null;
        string? firstPlayerId = null;

        for (var t = 0; t < 2; t++)
        {
            var teamId = await PostIdAsync(client, $"/api/v1/organisations/{orgId}/teams",
                new { name = $"Team {t}", shortName = $"T{t}" });
            var competitionTeamId = await PostIdAsync(client,
                $"/api/v1/organisations/{orgId}/competitions/{competitionId}/teams", new { teamId });
            competitionTeamIds.Add(competitionTeamId);

            var entries = new List<string>();
            for (var p = 0; p < playersPerTeam; p++)
            {
                var playerResponse = await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/registry/players",
                    new { firstName = $"P{p}", lastName = $"Team{t}", dateOfBirth = "2000-01-01", gender = "Male" });
                playerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
                var playerId = (await ReadJson(playerResponse)).GetProperty("playerId").GetString()!;

                // First player of the first team wears "7" so the jersey test has a known value.
                var jersey = t == 0 && p == 0 ? "7" : $"{t * 20 + p + 10}";
                var entryResponse = await client.PostAsJsonAsync(
                    $"/api/v1/organisations/{orgId}/competition-teams/{competitionTeamId}/roster",
                    new { playerId, jerseyNumber = jersey });
                entryResponse.StatusCode.Should().Be(HttpStatusCode.Created);
                var entryId = (await ReadJson(entryResponse)).GetProperty("id").GetString()!;
                entries.Add(entryId);

                if (t == 0 && p == 0)
                {
                    firstRosterEntryId = entryId;
                    firstPlayerId = playerId;
                }
            }

            perTeam.Add(entries);
        }

        var gameId = await PostIdAsync(client, $"/api/v1/organisations/{orgId}/competitions/{competitionId}/games",
            new
            {
                homeCompetitionTeamId = competitionTeamIds[0],
                awayCompetitionTeamId = competitionTeamIds[1],
                scheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            });

        return new GameContext(client, orgId, competitionId, gameId,
            perTeam.SelectMany(x => x).ToList(), perTeam, firstRosterEntryId!, firstPlayerId!);
    }

    private async Task<JsonElement> GetGameRosterAsync(GameContext ctx)
        => await ReadJson(await ctx.Client.GetAsync($"/api/v1/organisations/{ctx.OrgId}/games/{ctx.GameId}/roster"));

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
