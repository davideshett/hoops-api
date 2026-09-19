using FluentAssertions;
using Hoops.Modules.Registry.Domain;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.UnitTests.Registry;

public sealed class RegistryLedgerTests
{
    [Fact]
    public void An_entry_still_verifies_after_its_timestamp_loses_sub_microsecond_ticks()
    {
        // .NET clocks on Linux have 100 ns ticks; timestamptz keeps microseconds. The hash must be
        // over what the store returns, or every verify on a Linux host fails while macOS — whose clock
        // has no sub-microsecond ticks — never shows the problem.
        var tickPrecise = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_567); // .1234567 s
        var entry = RegistryLedgerEntry.Append(RegistryLedgerEntry.Genesis, LedgerEntryType.Registered, PlayerId.New(), tickPrecise);

        entry.OccurredAt.Ticks.Should().Be(tickPrecise.Ticks - 7, "the entry holds the microsecond-truncated instant");

        // Simulate the database round trip: what comes back is microsecond precision.
        var roundTripped = new DateTimeOffset(entry.OccurredAt.Ticks - (entry.OccurredAt.Ticks % 10), TimeSpan.Zero);
        var recomputed = RegistryLedgerEntry.ComputeEntryHash(entry.PreviousHash, entry.PayloadHash, roundTripped);

        recomputed.Should().Equal(entry.EntryHash);
    }

    [Fact]
    public void A_chain_of_entries_verifies_and_a_modified_timestamp_is_detected()
    {
        var previous = RegistryLedgerEntry.Genesis;
        var entries = new List<RegistryLedgerEntry>();
        for (var i = 0; i < 5; i++)
        {
            var entry = RegistryLedgerEntry.Append(previous, LedgerEntryType.Registered, PlayerId.New(),
                DateTimeOffset.UnixEpoch.AddSeconds(i).AddTicks(3));
            entries.Add(entry);
            previous = entry.EntryHash;
        }

        LedgerVerification.Verify(entries).IsValid.Should().BeTrue();
    }
}
