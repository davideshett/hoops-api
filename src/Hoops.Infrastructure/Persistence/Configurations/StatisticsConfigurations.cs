using Hoops.Modules.Competitions.Domain;
using Hoops.Modules.GameRecording.Domain;
using Hoops.Modules.Registry.Domain;
using Hoops.Modules.Statistics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hoops.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="PlayerGameStatline"/>.</summary>
public sealed class PlayerGameStatlineConfiguration : IEntityTypeConfiguration<PlayerGameStatline>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PlayerGameStatline> builder)
    {
        builder.ToTable("player_game_statlines");
        builder.HasKey(s => s.Id);

        // One line per player per game; a recompute replaces rather than duplicates.
        builder.HasIndex(s => new { s.GameId, s.GameRosterEntryId }).IsUnique();
        builder.HasIndex(s => new { s.CompetitionId, s.PlayerId });
        builder.HasIndex(s => s.PlayerId); // career queries span competitions
        builder.HasIndex(s => s.OrganisationId); // all-time records scan one organisation

        builder.HasOne<Game>().WithMany().HasForeignKey(s => s.GameId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Player>().WithMany().HasForeignKey(s => s.PlayerId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>EF mapping for <see cref="TeamGameStatline"/>.</summary>
public sealed class TeamGameStatlineConfiguration : IEntityTypeConfiguration<TeamGameStatline>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TeamGameStatline> builder)
    {
        builder.ToTable("team_game_statlines");
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.GameId, s.CompetitionTeamId }).IsUnique();
        builder.HasIndex(s => s.CompetitionId);
        builder.HasOne<Game>().WithMany().HasForeignKey(s => s.GameId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF mapping for <see cref="GamePeriodStateRow"/>.</summary>
public sealed class GamePeriodStateRowConfiguration : IEntityTypeConfiguration<GamePeriodStateRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GamePeriodStateRow> builder)
    {
        builder.ToTable("game_period_states");
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.GameId, s.CompetitionTeamId, s.Period }).IsUnique();
        builder.HasOne<Game>().WithMany().HasForeignKey(s => s.GameId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF mapping for <see cref="LineupStintRow"/>.</summary>
public sealed class LineupStintRowConfiguration : IEntityTypeConfiguration<LineupStintRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LineupStintRow> builder)
    {
        builder.ToTable("lineup_stints");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.PlayerIds).IsRequired();
        builder.HasIndex(s => s.GameId);
        builder.HasIndex(s => new { s.CompetitionTeamId, s.PlayerIds });
        builder.HasOne<Game>().WithMany().HasForeignKey(s => s.GameId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF mapping for <see cref="CompetitionPlayerAggregate"/>.</summary>
public sealed class CompetitionPlayerAggregateConfiguration : IEntityTypeConfiguration<CompetitionPlayerAggregate>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CompetitionPlayerAggregate> builder)
    {
        builder.ToTable("competition_player_aggregates");
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => new { a.CompetitionId, a.PlayerId }).IsUnique();

        // Leaderboards read qualified players ordered by a stat; the partial index keeps that cheap.
        builder.HasIndex(a => new { a.CompetitionId, a.Points }).HasFilter("is_qualified");

        // The career page reads every competition a player has appeared in.
        builder.HasIndex(a => a.PlayerId);

        builder.HasOne<Competition>().WithMany().HasForeignKey(a => a.CompetitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Player>().WithMany().HasForeignKey(a => a.PlayerId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// EF mapping for <see cref="PlayerCareerAggregate"/> — a registry entity (§13), deliberately NOT
/// tenant-scoped, because a career spans organisations.
/// </summary>
public sealed class PlayerCareerAggregateConfiguration : IEntityTypeConfiguration<PlayerCareerAggregate>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PlayerCareerAggregate> builder)
    {
        builder.ToTable("player_career_aggregates");
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.PlayerId).IsUnique();
        builder.HasOne<Player>().WithMany().HasForeignKey(a => a.PlayerId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>EF mapping for <see cref="CompetitionStanding"/>.</summary>
public sealed class CompetitionStandingConfiguration : IEntityTypeConfiguration<CompetitionStanding>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CompetitionStanding> builder)
    {
        builder.ToTable("competition_standings");
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.CompetitionId, s.CompetitionTeamId }).IsUnique();
        builder.HasIndex(s => new { s.CompetitionId, s.Position });
        builder.HasOne<Competition>().WithMany().HasForeignKey(s => s.CompetitionId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF mapping for <see cref="OutboxMessage"/> — infrastructure, not tenant data.</summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.MessageType).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();

        // The drainer reads pending messages oldest-first; the partial index keeps that a cheap scan
        // even once the processed backlog is large.
        builder.HasIndex(m => m.OccurredAt).HasFilter("processed_at IS NULL");
    }
}
