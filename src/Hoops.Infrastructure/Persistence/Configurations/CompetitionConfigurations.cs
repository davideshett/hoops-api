using System.Text.Json;
using Hoops.Modules.Competitions.Domain;
using Hoops.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hoops.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="Season"/>.</summary>
public sealed class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable("seasons");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired();
        builder.HasIndex(s => new { s.OrganisationId, s.Name }).IsUnique();
    }
}

/// <summary>EF mapping for <see cref="Competition"/>, including the jsonb rule set.</summary>
public sealed class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    private static readonly JsonSerializerOptions RuleSetJson = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Competition> builder)
    {
        builder.ToTable("competitions");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired();
        builder.Property(c => c.Slug).HasColumnType("citext").IsRequired();
        builder.Property(c => c.Format).HasConversion<string>().IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().IsRequired();
        builder.Property(c => c.Timezone).IsRequired();

        // rule_set: jsonb, serialised deterministically so change tracking stays honest.
        var comparer = new ValueComparer<RuleSet>(
            (a, b) => JsonSerializer.Serialize(a, RuleSetJson) == JsonSerializer.Serialize(b, RuleSetJson),
            v => JsonSerializer.Serialize(v, RuleSetJson).GetHashCode(),
            v => JsonSerializer.Deserialize<RuleSet>(JsonSerializer.Serialize(v, RuleSetJson), RuleSetJson)!);

        builder.Property(c => c.RuleSet)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, RuleSetJson),
                v => JsonSerializer.Deserialize<RuleSet>(v, RuleSetJson) ?? RuleSet.Fiba())
            .Metadata.SetValueComparer(comparer);

        builder.HasIndex(c => new { c.OrganisationId, c.SeasonId, c.Slug }).IsUnique();

        builder.HasOne<Season>().WithMany().HasForeignKey(c => c.SeasonId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>EF mapping for <see cref="Stage"/>.</summary>
public sealed class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable("stages");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired();
        builder.Property(s => s.StageType).HasConversion<string>().IsRequired();
        builder.HasIndex(s => new { s.CompetitionId, s.Sequence }).IsUnique();
        builder.HasOne<Competition>().WithMany().HasForeignKey(s => s.CompetitionId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF mapping for <see cref="Group"/>.</summary>
public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).IsRequired();
        builder.HasIndex(g => g.StageId);
        builder.HasOne<Stage>().WithMany().HasForeignKey(g => g.StageId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF mapping for <see cref="Team"/>.</summary>
public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired();
        builder.Property(t => t.ShortName).IsRequired();
        builder.Property(t => t.Abbreviation).HasColumnType("char(3)");
        builder.HasIndex(t => t.OrganisationId);
        builder.HasOne<Venue>().WithMany().HasForeignKey(t => t.HomeVenueId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>EF mapping for <see cref="CompetitionTeam"/>.</summary>
public sealed class CompetitionTeamConfiguration : IEntityTypeConfiguration<CompetitionTeam>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CompetitionTeam> builder)
    {
        builder.ToTable("competition_teams");
        builder.HasKey(ct => ct.Id);
        builder.Property(ct => ct.Status).HasConversion<string>().IsRequired();
        builder.HasIndex(ct => new { ct.CompetitionId, ct.TeamId }).IsUnique();

        builder.HasOne<Competition>().WithMany().HasForeignKey(ct => ct.CompetitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Team>().WithMany().HasForeignKey(ct => ct.TeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Group>().WithMany().HasForeignKey(ct => ct.GroupId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>EF mapping for <see cref="TeamStaff"/>.</summary>
public sealed class TeamStaffConfiguration : IEntityTypeConfiguration<TeamStaff>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TeamStaff> builder)
    {
        builder.ToTable("team_staff");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.FullName).IsRequired();
        builder.Property(s => s.Role).HasConversion<string>().IsRequired();
        builder.HasIndex(s => s.CompetitionTeamId);
        builder.HasOne<CompetitionTeam>().WithMany().HasForeignKey(s => s.CompetitionTeamId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF mapping for <see cref="Venue"/>.</summary>
public sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ToTable("venues");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).IsRequired();
        builder.Property(v => v.Timezone).IsRequired();
        builder.HasIndex(v => v.OrganisationId);
    }
}
