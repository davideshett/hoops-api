using System.Net;
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
        var verify = await admin.GetAsync("/api/v1/registry/ledger/verify");
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await ReadJson(verify);
        result.GetProperty("isValid").GetBoolean().Should().BeFalse();
        result.GetProperty("divergenceSequence").GetInt64().Should().Be(tamperedSequence);
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
