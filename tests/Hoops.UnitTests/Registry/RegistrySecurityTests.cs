using FluentAssertions;
using Hoops.Infrastructure.Security;
using Hoops.Modules.Registry.Domain;
using Hoops.SharedKernel.Identifiers;
using Microsoft.Extensions.Options;

namespace Hoops.UnitTests.Registry;

public sealed class RegistrySecurityTests
{
    private static HmacNinHasher Hasher() => new(Options.Create(new RegistryOptions { NinPepper = "unit-test-pepper" }));

    [Fact]
    public void Nin_hash_is_stable_and_normalises_punctuation()
    {
        var hasher = Hasher();

        var a = hasher.Hash("12345678901");
        var b = hasher.Hash("123-456-789 01");

        a.Should().NotBeNull();
        a.Should().Equal(b!, "digits-only normalisation makes the two inputs equivalent");
    }

    [Fact]
    public void Nin_hash_differs_for_different_numbers_and_is_null_when_absent()
    {
        var hasher = Hasher();

        hasher.Hash("12345678901").Should().NotEqual(hasher.Hash("10987654321")!);
        hasher.Hash(null).Should().BeNull();
        hasher.Hash("   ").Should().BeNull();
        hasher.Hash("no-digits").Should().BeNull();
    }

    [Fact]
    public void Different_peppers_produce_different_hashes()
    {
        var one = new HmacNinHasher(Options.Create(new RegistryOptions { NinPepper = "pepper-one" }));
        var two = new HmacNinHasher(Options.Create(new RegistryOptions { NinPepper = "pepper-two" }));

        one.Hash("12345678901").Should().NotEqual(two.Hash("12345678901")!);
    }

    [Fact]
    public void Identity_tier_is_derived_from_evidence_never_set()
    {
        var player = Player.Register("Ada", "Okafor", new DateOnly(2004, 3, 11), Gender.Female,
            OrganisationId.New(), UserId.New());

        player.IdentityTier.Should().Be(IdentityTier.Asserted, "no evidence yet");

        player.ApplyDobEvidence(DobEvidenceType.BirthCertificate, DateTimeOffset.UnixEpoch);
        player.IdentityTier.Should().Be(IdentityTier.Documented);

        player.ApplyNinVerification(verified: true, DateTimeOffset.UnixEpoch, "STUB", "ref");
        player.IdentityTier.Should().Be(IdentityTier.NinVerified);
    }

    [Fact]
    public void Anonymise_clears_identity_but_keeps_id_and_dob()
    {
        var dob = new DateOnly(2004, 3, 11);
        var player = Player.Register("Ada", "Okafor", dob, Gender.Female, OrganisationId.New(), UserId.New(),
            ninHmac: [1, 2, 3]);
        var id = player.Id;

        player.Anonymise(DateTimeOffset.UnixEpoch);

        player.Id.Should().Be(id, "the player_id is retained");
        player.DateOfBirth.Should().Be(dob, "sporting results depend on age");
        player.FirstName.Should().Be("REDACTED");
        player.LastName.Should().Be("REDACTED");
        player.NinHmac.Should().BeNull();
        player.IsAnonymised.Should().BeTrue();
    }

    [Fact]
    public void Ledger_chain_verifies_when_intact()
    {
        var entries = BuildChain(20);

        var result = LedgerVerification.Verify(entries);

        result.IsValid.Should().BeTrue();
        result.EntriesChecked.Should().Be(20);
        result.DivergenceSequence.Should().BeNull();
    }

    [Fact]
    public void Empty_ledger_is_valid()
        => LedgerVerification.Verify([]).IsValid.Should().BeTrue();

    private static List<RegistryLedgerEntry> BuildChain(int count)
    {
        var entries = new List<RegistryLedgerEntry>();
        var previous = RegistryLedgerEntry.Genesis;
        for (var i = 0; i < count; i++)
        {
            var entry = RegistryLedgerEntry.Append(previous, LedgerEntryType.Registered, PlayerId.New(),
                DateTimeOffset.UnixEpoch.AddSeconds(i));
            entries.Add(entry);
            previous = entry.EntryHash;
        }

        return entries;
    }
}
