using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Hoops.IntegrationTests;

public sealed class CompetitionFlowTests : IntegrationTestBase
{
    public CompetitionFlowTests(ApiFactory factory) : base(factory)
    {
    }

    // Every non-default RuleSet field, to prove the jsonb column round-trips without loss.
    private static readonly Dictionary<string, object> CustomRuleSet = new()
    {
        ["numberOfPeriods"] = 2,
        ["periodDurationSeconds"] = 480,
        ["overtimeDurationSeconds"] = 180,
        ["playersOnCourt"] = 3,
        ["minRosterSize"] = 3,
        ["maxRosterSize"] = 8,
        ["personalFoulLimit"] = 6,
        ["technicalFoulLimit"] = 1,
        ["teamFoulsUntilBonus"] = 4,
        ["timeoutsFirstHalf"] = 1,
        ["timeoutsSecondHalf"] = 2,
        ["timeoutsPerOvertime"] = 2,
        ["shotClockSeconds"] = 14,
        ["shotClockResetSeconds"] = 12,
        ["allowsTies"] = true,
        ["minimumIdentityTier"] = 2,
    };

    [Fact]
    public async Task Competition_custom_rule_set_round_trips_through_jsonb_without_loss()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);

        var seasonId = await CreateSeasonAsync(client, orgId);

        var create = await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/competitions", new
        {
            seasonId,
            name = "3x3 Invitational",
            slug = "3x3-invitational",
            format = "RoundRobin",
            category = "Senior Men",
            ruleSet = CustomRuleSet,
            timezone = "Africa/Lagos",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var competitionId = (await ReadJson(create)).GetProperty("id").GetString();

        // Fetch it back and assert every rule-set field survived the round trip.
        var fetched = await client.GetAsync($"/api/v1/organisations/{orgId}/competitions/{competitionId}");
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
        var ruleSet = (await ReadJson(fetched)).GetProperty("ruleSet");

        foreach (var (key, expected) in CustomRuleSet)
        {
            var actual = ruleSet.GetProperty(key);
            switch (expected)
            {
                case bool b:
                    actual.GetBoolean().Should().Be(b, "field {0} should round-trip", key);
                    break;
                case int i:
                    actual.GetInt32().Should().Be(i, "field {0} should round-trip", key);
                    break;
            }
        }
    }

    [Fact]
    public async Task Competition_created_without_a_rule_set_defaults_to_fiba()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var seasonId = await CreateSeasonAsync(client, orgId);

        var create = await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/competitions", new
        {
            seasonId, name = "League", slug = "premier-league", format = "League", timezone = "Africa/Lagos",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var ruleSet = (await ReadJson(create)).GetProperty("ruleSet");
        ruleSet.GetProperty("playersOnCourt").GetInt32().Should().Be(5);
        ruleSet.GetProperty("personalFoulLimit").GetInt32().Should().Be(5);
        ruleSet.GetProperty("shotClockSeconds").GetInt32().Should().Be(24);
        ruleSet.GetProperty("allowsTies").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task A_team_entered_in_two_competitions_appears_once_in_the_canonical_table()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);

        // Two seasons, a competition in each.
        var seasonA = await CreateSeasonAsync(client, orgId, "2024/25", "2024-10-01", "2025-06-30");
        var seasonB = await CreateSeasonAsync(client, orgId, "2025/26", "2025-10-01", "2026-06-30");
        var compA = await CreateCompetitionAsync(client, orgId, seasonA, "cup-2024");
        var compB = await CreateCompetitionAsync(client, orgId, seasonB, "cup-2025");

        // One canonical team.
        var teamCreate = await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/teams",
            new { name = "Lagos Islanders", shortName = "Islanders", abbreviation = "LAG" });
        var teamId = (await ReadJson(teamCreate)).GetProperty("id").GetString();

        // Enter the same team into both competitions.
        (await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/competitions/{compA}/teams", new { teamId }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/competitions/{compB}/teams", new { teamId }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // The canonical teams table still holds exactly one team.
        var teams = await client.GetAsync($"/api/v1/organisations/{orgId}/teams");
        (await ReadJson(teams)).GetArrayLength().Should().Be(1);

        // Both competitions reference it.
        var entriesA = await client.GetAsync($"/api/v1/organisations/{orgId}/competitions/{compA}/teams");
        var entriesB = await client.GetAsync($"/api/v1/organisations/{orgId}/competitions/{compB}/teams");
        (await ReadJson(entriesA))[0].GetProperty("teamId").GetString().Should().Be(teamId);
        (await ReadJson(entriesB))[0].GetProperty("teamId").GetString().Should().Be(teamId);
    }

    [Fact]
    public async Task Entering_the_same_team_twice_in_one_competition_conflicts()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var seasonId = await CreateSeasonAsync(client, orgId);
        var compId = await CreateCompetitionAsync(client, orgId, seasonId, "league");

        var teamCreate = await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/teams",
            new { name = "Kano Pillars", shortName = "Pillars" });
        var teamId = (await ReadJson(teamCreate)).GetProperty("id").GetString();

        (await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/competitions/{compId}/teams", new { teamId }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/competitions/{compId}/teams", new { teamId });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<string> CreateSeasonAsync(
        HttpClient client, Guid orgId, string name = "2025/26", string startsOn = "2025-10-01", string endsOn = "2026-06-30")
    {
        var create = await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/seasons",
            new { name, startsOn, endsOn });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadJson(create)).GetProperty("id").GetString()!;
    }

    private async Task<string> CreateCompetitionAsync(HttpClient client, Guid orgId, string seasonId, string slug)
    {
        var create = await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/competitions",
            new { seasonId, name = slug, slug, format = "League", timezone = "Africa/Lagos" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadJson(create)).GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
