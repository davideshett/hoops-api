using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hoops.Modules.Registry.Domain;
using Hoops.SharedKernel.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace Hoops.IntegrationTests;

public sealed class RegistryRosterAndMergeTests : IntegrationTestBase
{
    public RegistryRosterAndMergeTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Roster_rejects_a_body_with_a_name_instead_of_a_playerId()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var competitionTeamId = await CreateCompetitionTeamAsync(client, orgId);

        // A name and jersey, but no playerId — must be rejected.
        var response = await client.PostAsJsonAsync(RosterUrl(orgId, competitionTeamId),
            new { name = "Emeka Chukwu", jerseyNumber = "10" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Roster_is_built_from_a_playerId_with_jersey_uniqueness()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var competitionTeamId = await CreateCompetitionTeamAsync(client, orgId);
        var playerId = await CreatePlayerAsync(client, orgId);
        var otherPlayerId = await CreatePlayerAsync(client, orgId, "Bola", "Ade");

        var register = await client.PostAsJsonAsync(RosterUrl(orgId, competitionTeamId),
            new { playerId, jerseyNumber = "7", position = "PG", isCaptain = true });
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        // Same jersey → conflict.
        var dupJersey = await client.PostAsJsonAsync(RosterUrl(orgId, competitionTeamId),
            new { playerId = otherPlayerId, jerseyNumber = "7" });
        dupJersey.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Same player → conflict.
        var dupPlayer = await client.PostAsJsonAsync(RosterUrl(orgId, competitionTeamId),
            new { playerId, jerseyNumber = "8" });
        dupPlayer.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var roster = await client.GetAsync(RosterUrl(orgId, competitionTeamId));
        (await ReadJson(roster)).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Only_a_platform_admin_can_approve_a_merge()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var keep = await CreatePlayerAsync(client, orgId, "Ada", "Okafor");
        var merge = await CreatePlayerAsync(client, orgId, "Ada", "Okaforr");

        var propose = await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/registry/merge-proposals",
            new { keepId = keep, mergeId = merge, evidence = "same person, typo" });
        propose.StatusCode.Should().Be(HttpStatusCode.Created);
        var proposalId = (await ReadJson(propose)).GetProperty("id").GetString();

        // Org owner is not a platform admin → 403.
        var orgAdminApprove = await client.PostAsync($"/api/v1/registry/merge-proposals/{proposalId}/approve", null);
        orgAdminApprove.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Platform admin → 204.
        var admin = AuthenticatedClient(await NewPlatformAdminTokenAsync());
        var adminApprove = await admin.PostAsync($"/api/v1/registry/merge-proposals/{proposalId}/approve", null);
        adminApprove.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Merge_repoints_roster_entries_and_carries_the_higher_tier()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var competitionTeamId = await CreateCompetitionTeamAsync(client, orgId);

        var keep = await CreatePlayerAsync(client, orgId, "Ada", "Okafor");      // tier 0
        var merge = await CreatePlayerAsync(client, orgId, "Ada", "Okaforr");    // will be tier 1
        await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/registry/players/{merge}/dob-evidence",
            new { evidenceType = "BirthCertificate" });

        // Roster the duplicate.
        await client.PostAsJsonAsync(RosterUrl(orgId, competitionTeamId), new { playerId = merge, jerseyNumber = "9" });

        var propose = await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/registry/merge-proposals",
            new { keepId = keep, mergeId = merge, evidence = "duplicate" });
        var proposalId = (await ReadJson(propose)).GetProperty("id").GetString();

        var admin = AuthenticatedClient(await NewPlatformAdminTokenAsync());
        (await admin.PostAsync($"/api/v1/registry/merge-proposals/{proposalId}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The roster entry now points to the surviving player.
        var roster = await client.GetAsync(RosterUrl(orgId, competitionTeamId));
        (await ReadJson(roster))[0].GetProperty("playerId").GetString().Should().Be(keep);

        // The survivor carries the higher tier.
        var survivor = await client.GetAsync($"/api/v1/organisations/{orgId}/registry/players/{keep}");
        (await ReadJson(survivor)).GetProperty("identityTier").GetString().Should().Be("Documented");
    }

    [Fact]
    public async Task Merging_b_into_a_then_a_into_c_leaves_no_two_hop_lookups()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var a = await CreatePlayerAsync(client, orgId, "Name", "A");
        var b = await CreatePlayerAsync(client, orgId, "Name", "B");
        var c = await CreatePlayerAsync(client, orgId, "Name", "C");
        var admin = AuthenticatedClient(await NewPlatformAdminTokenAsync());

        await ApproveMergeAsync(client, admin, orgId, keep: a, merge: b); // B -> A
        await ApproveMergeAsync(client, admin, orgId, keep: c, merge: a); // A -> C

        // Both A and B must point directly to C — no two-hop through A.
        await WithDbAsync(async db =>
        {
            var survivorOfA = await MergedIntoAsync(db, a);
            var survivorOfB = await MergedIntoAsync(db, b);
            survivorOfA.Should().Be(c);
            survivorOfB.Should().Be(c, "the B -> A pointer was re-resolved to C on the second merge");
        });
    }

    private async Task ApproveMergeAsync(HttpClient proposer, HttpClient admin, Guid orgId, string keep, string merge)
    {
        var propose = await proposer.PostAsJsonAsync($"/api/v1/organisations/{orgId}/registry/merge-proposals",
            new { keepId = keep, mergeId = merge, evidence = "duplicate" });
        propose.StatusCode.Should().Be(HttpStatusCode.Created);
        var proposalId = (await ReadJson(propose)).GetProperty("id").GetString();
        (await admin.PostAsync($"/api/v1/registry/merge-proposals/{proposalId}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task<string?> MergedIntoAsync(Hoops.Infrastructure.Persistence.AppDbContext db, string playerId)
    {
        var id = PlayerId.FromGuid(Guid.Parse(playerId));
        var player = await db.Players.IgnoreQueryFilters().FirstAsync(p => p.Id == id);
        return player.MergedIntoId?.Value.ToString();
    }

    private static string RosterUrl(Guid orgId, string competitionTeamId)
        => $"/api/v1/organisations/{orgId}/competition-teams/{competitionTeamId}/roster";

    private async Task<string> CreatePlayerAsync(HttpClient client, Guid orgId, string first = "Ada", string last = "Okafor")
    {
        var response = await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/registry/players",
            new { firstName = first, lastName = last, dateOfBirth = "2004-03-11", gender = "Female" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadJson(response)).GetProperty("playerId").GetString()!;
    }

    private async Task<string> CreateCompetitionTeamAsync(HttpClient client, Guid orgId)
    {
        var season = (await ReadJson(await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/seasons",
            new { name = "2025/26", startsOn = "2025-10-01", endsOn = "2026-06-30" }))).GetProperty("id").GetString();
        var comp = (await ReadJson(await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/competitions",
            new { seasonId = season, name = "League", slug = UniqueSlug(), format = "League", timezone = "Africa/Lagos" }))).GetProperty("id").GetString();
        var team = (await ReadJson(await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/teams",
            new { name = "Team", shortName = "Team" }))).GetProperty("id").GetString();
        var entry = await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/competitions/{comp}/teams", new { teamId = team });
        return (await ReadJson(entry)).GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
