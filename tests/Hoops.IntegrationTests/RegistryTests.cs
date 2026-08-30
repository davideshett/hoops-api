using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hoops.Api.Logging;
using Microsoft.EntityFrameworkCore;

namespace Hoops.IntegrationTests;

public sealed class RegistryTests : IntegrationTestBase
{
    public RegistryTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task A_plaintext_nin_never_appears_in_logs_or_the_audit_query_terms()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        const string nin = "19283746501";
        CapturingSink.Clear();

        var response = await AuthenticatedClient(token).PostAsJsonAsync(PlayersUrl(orgId), new
        {
            firstName = "Ada", lastName = "Okafor", dateOfBirth = "2004-03-11", gender = "Female", nin,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Not in any log line...
        CapturingSink.ContainsText(nin).Should().BeFalse("the plaintext NIN must never be logged");

        // ...and not in any audit query_terms.
        await WithDbAsync(async db =>
        {
            var audits = await db.RegistryAudits.ToListAsync();
            audits.Should().NotBeEmpty();
            audits.Any(a => JsonSerializer.Serialize(a.QueryTerms).Contains(nin)).Should().BeFalse();
        });

        // ...and the response body carries no NIN field.
        (await response.Content.ReadAsStringAsync()).Should().NotContain(nin);
    }

    [Fact]
    public async Task Two_players_with_the_same_nin_collide()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        const string nin = "55544433322";

        var first = await client.PostAsJsonAsync(PlayersUrl(orgId),
            new { firstName = "Chidi", lastName = "Eze", dateOfBirth = "2000-01-01", gender = "Male", nin });
        var firstId = (await ReadJson(first)).GetProperty("playerId").GetString();

        var second = await client.PostAsJsonAsync(PlayersUrl(orgId),
            new { firstName = "Chidi", lastName = "Eze", dateOfBirth = "2000-01-01", gender = "Male", nin });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await second.Content.ReadAsStringAsync();
        body.Should().Contain("PLAYER_ALREADY_REGISTERED");
        body.Should().Contain(firstId!, "the existing playerId is returned");
    }

    [Fact]
    public async Task A_player_can_be_registered_without_a_nin_at_tier_0()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();

        var response = await AuthenticatedClient(token).PostAsJsonAsync(PlayersUrl(orgId),
            new { firstName = "Ngozi", lastName = "Bello", dateOfBirth = "1999-05-20", gender = "Female" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJson(response)).GetProperty("identityTier").GetString().Should().Be("Asserted");
    }

    [Fact]
    public async Task Identity_tier_moves_only_when_evidence_changes()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var playerId = await CreatePlayerAsync(client, orgId);

        var evidence = await client.PostAsJsonAsync($"{PlayersUrl(orgId)}/{playerId}/dob-evidence",
            new { evidenceType = "BirthCertificate" });
        evidence.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(evidence)).GetProperty("identityTier").GetString().Should().Be("Documented");
    }

    [Fact]
    public async Task Search_that_is_too_broad_is_rejected_and_a_valid_search_writes_one_audit_row()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        await client.PostAsJsonAsync(PlayersUrl(orgId),
            new { firstName = "Emeka", lastName = "Zamani", dateOfBirth = "2001-07-07", gender = "Male" });

        var tooBroad = await client.PostAsJsonAsync($"{PlayersUrl(orgId)}/search", new { lastName = "Z" });
        tooBroad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJson(tooBroad)).GetProperty("code").GetString().Should().Be("SEARCH_TOO_BROAD");

        var before = await CountSearchAuditsAsync();
        var ok = await client.PostAsJsonAsync($"{PlayersUrl(orgId)}/search",
            new { lastName = "Zamani", dateOfBirth = "2001-07-07" });
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(ok)).GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        (await CountSearchAuditsAsync()).Should().Be(before + 1, "exactly one audit row per search");
    }

    [Fact]
    public async Task Registering_a_minor_without_guardian_consent_fails()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var minorDob = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10).ToString("yyyy-MM-dd");

        var withoutConsent = await client.PostAsJsonAsync(PlayersUrl(orgId),
            new { firstName = "Tunde", lastName = "Junior", dateOfBirth = minorDob, gender = "Male" });
        withoutConsent.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJson(withoutConsent)).GetProperty("code").GetString().Should().Be("GUARDIAN_CONSENT_REQUIRED");

        var withConsent = await client.PostAsJsonAsync(PlayersUrl(orgId), new
        {
            firstName = "Tunde", lastName = "Junior", dateOfBirth = minorDob, gender = "Male",
            guardianConsent = new { guardianName = "Parent", guardianPhone = "08000000000", scopeVersion = "v1-2026-08" },
        });
        withConsent.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Photo_urls_are_presigned_and_the_object_key_never_reaches_the_client()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var playerId = await CreatePlayerAsync(client, orgId);

        var upload = await client.PostAsync($"{PlayersUrl(orgId)}/{playerId}/photo", content: null);
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadDoc = await ReadJson(upload);
        uploadDoc.GetProperty("url").GetString().Should().Contain("expires=");
        uploadDoc.GetProperty("expiresAt").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow);

        var read = await client.GetAsync($"{PlayersUrl(orgId)}/{playerId}/photo");
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        // The private object key is never a field on any response.
        (await read.Content.ReadAsStringAsync()).Should().NotContain("photoObjectKey");
    }

    [Fact]
    public async Task Anonymising_clears_identity_but_keeps_the_player()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var playerId = await CreatePlayerAsync(client, orgId, "Kemi", "Adeyemi");

        var admin = AuthenticatedClient(await NewPlatformAdminTokenAsync());
        var anonymise = await admin.PostAsync($"/api/v1/registry/players/{playerId}/anonymise", content: null);
        anonymise.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await client.GetAsync($"{PlayersUrl(orgId)}/{playerId}");
        after.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadJson(after);
        dto.GetProperty("playerId").GetString().Should().Be(playerId, "the id is retained");
        dto.GetProperty("fullName").GetString().Should().Contain("REDACTED");
    }

    [Fact]
    public async Task Ledger_verify_is_valid_after_normal_activity()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        await CreatePlayerAsync(AuthenticatedClient(token), orgId);

        var admin = AuthenticatedClient(await NewPlatformAdminTokenAsync());
        var verify = await admin.GetAsync("/api/v1/registry/ledger/verify");
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(verify)).GetProperty("isValid").GetBoolean().Should().BeTrue();
    }

    private static string PlayersUrl(Guid orgId) => $"/api/v1/organisations/{orgId}/registry/players";

    private async Task<string> CreatePlayerAsync(HttpClient client, Guid orgId, string first = "Ada", string last = "Okafor")
    {
        var response = await client.PostAsJsonAsync(PlayersUrl(orgId),
            new { firstName = first, lastName = last, dateOfBirth = "2004-03-11", gender = "Female" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadJson(response)).GetProperty("playerId").GetString()!;
    }

    private async Task<int> CountSearchAuditsAsync()
    {
        var count = 0;
        await WithDbAsync(async db =>
            count = await db.RegistryAudits.CountAsync(a => a.Action == Hoops.Modules.Registry.Domain.RegistryAction.Search));
        return count;
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
