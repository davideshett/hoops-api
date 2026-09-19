using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hoops.Modules.Registry.Domain;
using Hoops.SharedKernel.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace Hoops.IntegrationTests;

public sealed class LedgerTamperTests : IntegrationTestBase
{
    public LedgerTamperTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Appending_1000_entries_then_tampering_one_reports_the_exact_divergence()
    {
        var admin = AuthenticatedClient(await NewPlatformAdminTokenAsync());

        // Append 1000 entries chained onto the current ledger tip.
        long tamperedSequence = 0;
        await WithDbAsync(async db =>
        {
            var last = await db.RegistryLedger.OrderByDescending(e => e.Sequence).FirstOrDefaultAsync();
            var previous = last?.EntryHash ?? RegistryLedgerEntry.Genesis;

            var entries = new List<RegistryLedgerEntry>();
            for (var i = 0; i < 1000; i++)
            {
                var entry = RegistryLedgerEntry.Append(previous, LedgerEntryType.Registered, PlayerId.New(),
                    DateTimeOffset.UnixEpoch.AddSeconds(i));
                entries.Add(entry);
                previous = entry.EntryHash;
            }

            db.RegistryLedger.AddRange(entries);
            await db.SaveChangesAsync();
            tamperedSequence = entries[500].Sequence; // capture the DB-assigned sequence
        });

        // Intact so far.
        (await ReadJson(await admin.GetAsync("/api/v1/registry/ledger/verify")))
            .GetProperty("isValid").GetBoolean().Should().BeTrue();

        // Tamper with one row's occurred-at (part of the entry hash) without recomputing.
        await WithDbAsync(async db =>
        {
            var entry = await db.RegistryLedger.FirstAsync(e => e.Sequence == tamperedSequence);
            db.Entry(entry).Property(e => e.OccurredAt).CurrentValue = entry.OccurredAt.AddSeconds(9999);
            await db.SaveChangesAsync();
        });

        // Verify now reports the exact divergence point.
        try
        {
            var verify = await admin.GetAsync("/api/v1/registry/ledger/verify");
            verify.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await ReadJson(verify);
            result.GetProperty("isValid").GetBoolean().Should().BeFalse();
            result.GetProperty("divergenceSequence").GetInt64().Should().Be(tamperedSequence);
        }
        finally
        {
            // The ledger is shared with every other test in the collection: put it back.
            await WithDbAsync(async db =>
            {
                var entry = await db.RegistryLedger.FirstAsync(e => e.Sequence == tamperedSequence);
                db.Entry(entry).Property(e => e.OccurredAt).CurrentValue = entry.OccurredAt.AddSeconds(-9999);
                await db.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task Concurrent_registrations_do_not_fork_the_chain()
    {
        // Two appenders that read the same tip would both hash against it and fork the chain, after
        // which every verify reports tampering that never happened. First seen when the 8-court load
        // test registered 80 players at once. Appends are serialised with an advisory lock.
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var admin = AuthenticatedClient(await NewPlatformAdminTokenAsync());

        var registrations = Enumerable.Range(0, 20).Select(i => client.PostAsJsonAsync(
            $"/api/v1/organisations/{orgId}/registry/players",
            new { firstName = $"Concurrent{i}", lastName = "Appender", dateOfBirth = "2000-01-01", gender = "Male" }));
        var responses = await Task.WhenAll(registrations);
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);

        var result = await ReadJson(await admin.GetAsync("/api/v1/registry/ledger/verify"));
        result.GetProperty("isValid").GetBoolean().Should().BeTrue(
            "twenty simultaneous appends must produce one chain, not a fork");
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
