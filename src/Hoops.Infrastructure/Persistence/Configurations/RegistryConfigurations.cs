using System.Text.Json;
using Hoops.Modules.Competitions.Domain;
using Hoops.Modules.Registry.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hoops.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for the platform-level <see cref="Player"/> (ADR-003: no organisation_id, unfiltered).</summary>
public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("players");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.FirstName).IsRequired();
        builder.Property(p => p.LastName).IsRequired();
        builder.Property(p => p.Nationality).HasColumnType("char(2)").IsRequired();
        builder.Property(p => p.Gender).HasConversion<string>().IsRequired();
        builder.Property(p => p.IdentityTier).HasConversion<string>().IsRequired();
        builder.Property(p => p.DobEvidenceType).HasConversion<string>().IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().IsRequired();
        builder.Property(p => p.NinHmac).HasColumnType("bytea");

        // Exact-match NIN de-duplication without holding the value: unique among live, non-merged records.
        builder.HasIndex(p => p.NinHmac).IsUnique().HasFilter("nin_hmac IS NOT NULL AND merged_into_id IS NULL");
        builder.HasIndex(p => new { p.LastName, p.DateOfBirth });
        builder.HasIndex(p => p.RegisteredByOrganisationId);

        // Self-reference for merges.
        builder.HasOne<Player>().WithMany().HasForeignKey(p => p.MergedIntoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => p.DeletedAt == null);
    }
}

/// <summary>EF mapping for <see cref="PlayerOrgLink"/> (registry entity — unfiltered).</summary>
public sealed class PlayerOrgLinkConfiguration : IEntityTypeConfiguration<PlayerOrgLink>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PlayerOrgLink> builder)
    {
        builder.ToTable("player_org_links");
        builder.HasKey(l => l.Id);
        builder.HasIndex(l => new { l.PlayerId, l.OrganisationId }).IsUnique();
        builder.HasOne<Player>().WithMany().HasForeignKey(l => l.PlayerId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF mapping for <see cref="ConsentRecord"/> (registry entity — unfiltered).</summary>
public sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("consent_records");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ConsentType).HasConversion<string>().IsRequired();
        builder.Property(c => c.ScopeVersion).IsRequired();
        builder.HasIndex(c => c.PlayerId);
        builder.HasOne<Player>().WithMany().HasForeignKey(c => c.PlayerId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF mapping for <see cref="PlayerEligibilityFlag"/> (registry entity — unfiltered).</summary>
public sealed class PlayerEligibilityFlagConfiguration : IEntityTypeConfiguration<PlayerEligibilityFlag>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PlayerEligibilityFlag> builder)
    {
        builder.ToTable("player_eligibility_flags");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.FlagType).HasConversion<string>().IsRequired();
        builder.Property(f => f.Scope).HasConversion<string>().IsRequired();
        builder.Property(f => f.Reason).IsRequired();
        builder.HasIndex(f => f.PlayerId).HasFilter("resolved_at IS NULL");
        builder.HasOne<Player>().WithMany().HasForeignKey(f => f.PlayerId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF mapping for <see cref="RegistryAudit"/> (registry entity — unfiltered). query_terms never holds a raw NIN.</summary>
public sealed class RegistryAuditConfiguration : IEntityTypeConfiguration<RegistryAudit>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RegistryAudit> builder)
    {
        builder.ToTable("registry_audit");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasConversion<string>().IsRequired();

        var comparer = new ValueComparer<Dictionary<string, string>>(
            (x, y) => JsonSerializer.Serialize(x, Json) == JsonSerializer.Serialize(y, Json),
            v => JsonSerializer.Serialize(v, Json).GetHashCode(),
            v => new Dictionary<string, string>(v));

        builder.Property(a => a.QueryTerms)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, Json) ?? new Dictionary<string, string>())
            .Metadata.SetValueComparer(comparer);

        builder.HasIndex(a => a.PlayerId);
        builder.HasIndex(a => a.OccurredAt);
    }
}

/// <summary>EF mapping for the hash-chained <see cref="RegistryLedgerEntry"/> (registry entity — unfiltered).</summary>
public sealed class RegistryLedgerEntryConfiguration : IEntityTypeConfiguration<RegistryLedgerEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RegistryLedgerEntry> builder)
    {
        builder.ToTable("registry_ledger");
        builder.HasKey(e => e.Sequence);
        builder.Property(e => e.Sequence).ValueGeneratedOnAdd();
        builder.Property(e => e.EntryType).HasConversion<string>().IsRequired();
        builder.Property(e => e.PayloadHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.PreviousHash).HasColumnType("bytea").IsRequired();
        builder.Property(e => e.EntryHash).HasColumnType("bytea").IsRequired();
        builder.HasIndex(e => e.EntryHash).IsUnique();
    }
}

/// <summary>EF mapping for <see cref="MergeProposal"/> (platform queue — unfiltered).</summary>
public sealed class MergeProposalConfiguration : IEntityTypeConfiguration<MergeProposal>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MergeProposal> builder)
    {
        builder.ToTable("merge_proposals");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Evidence).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().IsRequired();
        builder.HasIndex(m => m.Status);
    }
}

/// <summary>EF mapping for the tenant-scoped <see cref="RosterEntry"/>.</summary>
public sealed class RosterEntryConfiguration : IEntityTypeConfiguration<RosterEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RosterEntry> builder)
    {
        builder.ToTable("roster_entries");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.JerseyNumber).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().IsRequired();

        // Jersey unique per squad among non-removed entries; a player appears at most once per squad.
        builder.HasIndex(r => new { r.CompetitionTeamId, r.JerseyNumber }).IsUnique().HasFilter("status <> 'Removed'");
        builder.HasIndex(r => new { r.CompetitionTeamId, r.PlayerId }).IsUnique();

        builder.HasOne<CompetitionTeam>().WithMany().HasForeignKey(r => r.CompetitionTeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Player>().WithMany().HasForeignKey(r => r.PlayerId).OnDelete(DeleteBehavior.Restrict);
    }
}
