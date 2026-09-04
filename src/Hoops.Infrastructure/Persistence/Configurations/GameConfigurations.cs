using System.Text.Json;
using Hoops.Modules.Competitions.Domain;
using Hoops.Modules.GameRecording.Domain;
using Hoops.Modules.Registry.Domain;
using Hoops.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hoops.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="Game"/>, including the frozen jsonb rule-set snapshot.</summary>
public sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("games");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Status).HasConversion<string>().IsRequired();

        var converter = new ValueConverter<RuleSet?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, Json),
            v => v == null ? null : JsonSerializer.Deserialize<RuleSet>(v, Json));
        var comparer = new ValueComparer<RuleSet?>(
            (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
            v => v == null ? 0 : JsonSerializer.Serialize(v, Json).GetHashCode(),
            v => v);

        builder.Property(g => g.RuleSetSnapshot).HasColumnType("jsonb").HasConversion(converter);
        builder.Property(g => g.RuleSetSnapshot).Metadata.SetValueComparer(comparer);

        builder.HasIndex(g => new { g.CompetitionId, g.ScheduledAt });
        builder.HasIndex(g => g.StageId);

        builder.HasOne<Competition>().WithMany().HasForeignKey(g => g.CompetitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CompetitionTeam>().WithMany().HasForeignKey(g => g.HomeCompetitionTeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompetitionTeam>().WithMany().HasForeignKey(g => g.AwayCompetitionTeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Venue>().WithMany().HasForeignKey(g => g.VenueId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>EF mapping for the frozen <see cref="GameRosterEntry"/> snapshot.</summary>
public sealed class GameRosterEntryConfiguration : IEntityTypeConfiguration<GameRosterEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GameRosterEntry> builder)
    {
        builder.ToTable("game_roster_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.JerseyNumber).IsRequired();
        builder.HasIndex(e => e.GameId);
        builder.HasIndex(e => new { e.GameId, e.CompetitionTeamId, e.JerseyNumber }).IsUnique();

        builder.HasOne<Game>().WithMany().HasForeignKey(e => e.GameId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Player>().WithMany().HasForeignKey(e => e.PlayerId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>EF mapping for <see cref="GameOfficial"/>.</summary>
public sealed class GameOfficialConfiguration : IEntityTypeConfiguration<GameOfficial>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GameOfficial> builder)
    {
        builder.ToTable("game_officials");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.FullName).IsRequired();
        builder.Property(o => o.Role).HasConversion<string>().IsRequired();
        builder.HasIndex(o => o.GameId);
        builder.HasOne<Game>().WithMany().HasForeignKey(o => o.GameId).OnDelete(DeleteBehavior.Cascade);
    }
}
