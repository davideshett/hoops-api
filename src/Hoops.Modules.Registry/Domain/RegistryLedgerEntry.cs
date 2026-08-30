using System.Security.Cryptography;
using System.Text;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Registry.Domain;

/// <summary>
/// One tamper-evident, hash-chained entry in the provenance ledger (§5A.6). The payload is minimal and
/// non-personal — entry type, player id, and hashes — so the ledger can never reconstruct personal
/// data after an NDPA erasure. <c>entry_hash = SHA256(previous_hash || payload_hash || occurred_at)</c>.
/// </summary>
public sealed class RegistryLedgerEntry
{
    /// <summary>The genesis previous-hash (32 zero bytes) for the first entry.</summary>
    public static readonly byte[] Genesis = new byte[32];

    private RegistryLedgerEntry()
    {
        PayloadHash = null!;
        PreviousHash = null!;
        EntryHash = null!;
    }

    private RegistryLedgerEntry(
        LedgerEntryType entryType, PlayerId? playerId, byte[] payloadHash, byte[] previousHash,
        byte[] entryHash, DateTimeOffset occurredAt)
    {
        EntryType = entryType;
        PlayerId = playerId;
        PayloadHash = payloadHash;
        PreviousHash = previousHash;
        EntryHash = entryHash;
        OccurredAt = occurredAt;
    }

    /// <summary>Monotonic sequence (bigserial), assigned by the database.</summary>
    public long Sequence { get; private set; }

    /// <summary>What kind of entry this is.</summary>
    public LedgerEntryType EntryType { get; private set; }

    /// <summary>The player this entry concerns, if any.</summary>
    public PlayerId? PlayerId { get; private set; }

    /// <summary>SHA-256 of the canonical (non-personal) payload.</summary>
    public byte[] PayloadHash { get; private set; }

    /// <summary>The previous entry's <see cref="EntryHash"/> (or <see cref="Genesis"/> for the first).</summary>
    public byte[] PreviousHash { get; private set; }

    /// <summary>SHA-256 over previous hash, payload hash, and occurred-at.</summary>
    public byte[] EntryHash { get; private set; }

    /// <summary>When the entry occurred, UTC.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Appends a new entry chained onto <paramref name="previousHash"/>.</summary>
    public static RegistryLedgerEntry Append(
        byte[] previousHash, LedgerEntryType entryType, PlayerId? playerId, DateTimeOffset occurredAt)
    {
        var payloadHash = ComputePayloadHash(entryType, playerId);
        var entryHash = ComputeEntryHash(previousHash, payloadHash, occurredAt);
        return new RegistryLedgerEntry(entryType, playerId, payloadHash, previousHash, entryHash, occurredAt);
    }

    /// <summary>SHA-256 of the canonical payload: entry type and player id only.</summary>
    public static byte[] ComputePayloadHash(LedgerEntryType entryType, PlayerId? playerId)
    {
        var canonical = $"{(int)entryType}|{playerId?.Value.ToString() ?? string.Empty}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
    }

    /// <summary><c>SHA256(previous_hash || payload_hash || occurred_at)</c>.</summary>
    public static byte[] ComputeEntryHash(byte[] previousHash, byte[] payloadHash, DateTimeOffset occurredAt)
    {
        var occ = Encoding.UTF8.GetBytes(occurredAt.UtcDateTime.ToString("O"));
        var buffer = new byte[previousHash.Length + payloadHash.Length + occ.Length];
        Buffer.BlockCopy(previousHash, 0, buffer, 0, previousHash.Length);
        Buffer.BlockCopy(payloadHash, 0, buffer, previousHash.Length, payloadHash.Length);
        Buffer.BlockCopy(occ, 0, buffer, previousHash.Length + payloadHash.Length, occ.Length);
        return SHA256.HashData(buffer);
    }
}

/// <summary>The outcome of verifying the ledger chain.</summary>
/// <param name="IsValid">True if the whole chain is intact.</param>
/// <param name="DivergenceSequence">The first sequence where the chain breaks, or null if valid.</param>
/// <param name="EntriesChecked">How many entries were checked.</param>
public sealed record LedgerVerificationResult(bool IsValid, long? DivergenceSequence, int EntriesChecked);

/// <summary>Recomputes and validates the ledger hash chain (§5A.6).</summary>
public static class LedgerVerification
{
    /// <summary>
    /// Walks <paramref name="entries"/> in sequence order, recomputing each entry hash and checking the
    /// chain link. Reports the first divergent sequence, or a valid result.
    /// </summary>
    public static LedgerVerificationResult Verify(IReadOnlyList<RegistryLedgerEntry> entries)
    {
        var previous = RegistryLedgerEntry.Genesis;
        var count = 0;
        foreach (var entry in entries.OrderBy(e => e.Sequence))
        {
            count++;
            var recomputed = RegistryLedgerEntry.ComputeEntryHash(entry.PreviousHash, entry.PayloadHash, entry.OccurredAt);
            var linkOk = entry.PreviousHash.AsSpan().SequenceEqual(previous);
            var hashOk = entry.EntryHash.AsSpan().SequenceEqual(recomputed);
            if (!linkOk || !hashOk)
            {
                return new LedgerVerificationResult(false, entry.Sequence, count);
            }

            previous = entry.EntryHash;
        }

        return new LedgerVerificationResult(true, null, count);
    }
}
