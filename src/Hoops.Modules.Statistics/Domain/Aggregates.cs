using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Statistics.Domain;

/// <summary>Totals a player accumulated across one competition. Rebuilt wholesale from statlines.</summary>
public sealed class CompetitionPlayerAggregate : ITenantScoped, IAuditableEntity
{
    private CompetitionPlayerAggregate()
    {
    }

    /// <summary>Primary key.</summary>
    public CompetitionPlayerAggregateId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The competition.</summary>
    public CompetitionId CompetitionId { get; private set; }

    /// <summary>The registry player.</summary>
    public PlayerId PlayerId { get; private set; }

    /// <summary>Games the player appeared in.</summary>
    public int GamesPlayed { get; private set; }

    /// <summary>Total points.</summary>
    public int Points { get; private set; }

    /// <summary>Field goals made.</summary>
    public int FieldGoalsMade { get; private set; }

    /// <summary>Field goals attempted.</summary>
    public int FieldGoalsAttempted { get; private set; }

    /// <summary>Three-pointers made.</summary>
    public int ThreePointersMade { get; private set; }

    /// <summary>Three-pointers attempted.</summary>
    public int ThreePointersAttempted { get; private set; }

    /// <summary>Free throws made.</summary>
    public int FreeThrowsMade { get; private set; }

    /// <summary>Free throws attempted.</summary>
    public int FreeThrowsAttempted { get; private set; }

    /// <summary>Offensive rebounds.</summary>
    public int OffensiveRebounds { get; private set; }

    /// <summary>Defensive rebounds.</summary>
    public int DefensiveRebounds { get; private set; }

    /// <summary>Assists.</summary>
    public int Assists { get; private set; }

    /// <summary>Steals.</summary>
    public int Steals { get; private set; }

    /// <summary>Blocks.</summary>
    public int Blocks { get; private set; }

    /// <summary>Turnovers.</summary>
    public int Turnovers { get; private set; }

    /// <summary>Fouls committed.</summary>
    public int FoulsCommitted { get; private set; }

    /// <summary>Seconds played.</summary>
    public int SecondsPlayed { get; private set; }

    /// <summary>Plus/minus.</summary>
    public int PlusMinus { get; private set; }

    /// <summary>
    /// Whether the player has appeared in enough games for per-game leaderboards. This shifts as the
    /// competition progresses, so it is recomputed for EVERY player whenever any game finalises (§9.3).
    /// </summary>
    public bool IsQualified { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Total rebounds.</summary>
    public int TotalRebounds => OffensiveRebounds + DefensiveRebounds;

    /// <summary>Points per game, or null when no games played.</summary>
    public double? PointsPerGame => GamesPlayed == 0 ? null : (double)Points / GamesPlayed;

    /// <summary>Builds an aggregate from a player's statlines in one competition.</summary>
    public static CompetitionPlayerAggregate FromStatlines(
        OrganisationId organisationId, CompetitionId competitionId, PlayerId playerId,
        IReadOnlyList<PlayerGameStatline> lines)
        => new()
        {
            Id = CompetitionPlayerAggregateId.New(),
            OrganisationId = organisationId,
            CompetitionId = competitionId,
            PlayerId = playerId,
            GamesPlayed = lines.Count,
            Points = lines.Sum(l => l.Points),
            FieldGoalsMade = lines.Sum(l => l.FieldGoalsMade),
            FieldGoalsAttempted = lines.Sum(l => l.FieldGoalsAttempted),
            ThreePointersMade = lines.Sum(l => l.ThreePointersMade),
            ThreePointersAttempted = lines.Sum(l => l.ThreePointersAttempted),
            FreeThrowsMade = lines.Sum(l => l.FreeThrowsMade),
            FreeThrowsAttempted = lines.Sum(l => l.FreeThrowsAttempted),
            OffensiveRebounds = lines.Sum(l => l.OffensiveRebounds),
            DefensiveRebounds = lines.Sum(l => l.DefensiveRebounds),
            Assists = lines.Sum(l => l.Assists),
            Steals = lines.Sum(l => l.Steals),
            Blocks = lines.Sum(l => l.Blocks),
            Turnovers = lines.Sum(l => l.Turnovers),
            FoulsCommitted = lines.Sum(l => l.FoulsCommitted),
            SecondsPlayed = lines.Sum(l => l.SecondsPlayed),
            PlusMinus = lines.Sum(l => l.PlusMinus),
        };

    /// <summary>Sets the qualification flag against the competition-wide threshold.</summary>
    public void SetQualified(bool qualified) => IsQualified = qualified;
}

/// <summary>
/// A player's career totals across EVERY organisation and competition. Deliberately NOT tenant-scoped
/// (§13): identity spans organisations, so a career must too — a player at two clubs under two
/// organisers has one career, not two.
/// </summary>
public sealed class PlayerCareerAggregate : IAuditableEntity
{
    private PlayerCareerAggregate()
    {
    }

    /// <summary>Primary key.</summary>
    public PlayerCareerAggregateId Id { get; private set; }

    /// <summary>The registry player.</summary>
    public PlayerId PlayerId { get; private set; }

    /// <summary>Games played across all competitions.</summary>
    public int GamesPlayed { get; private set; }

    /// <summary>Competitions appeared in.</summary>
    public int CompetitionsPlayed { get; private set; }

    /// <summary>Total points.</summary>
    public int Points { get; private set; }

    /// <summary>Field goals made.</summary>
    public int FieldGoalsMade { get; private set; }

    /// <summary>Field goals attempted.</summary>
    public int FieldGoalsAttempted { get; private set; }

    /// <summary>Three-pointers made.</summary>
    public int ThreePointersMade { get; private set; }

    /// <summary>Three-pointers attempted.</summary>
    public int ThreePointersAttempted { get; private set; }

    /// <summary>Free throws made.</summary>
    public int FreeThrowsMade { get; private set; }

    /// <summary>Free throws attempted.</summary>
    public int FreeThrowsAttempted { get; private set; }

    /// <summary>Offensive rebounds.</summary>
    public int OffensiveRebounds { get; private set; }

    /// <summary>Defensive rebounds.</summary>
    public int DefensiveRebounds { get; private set; }

    /// <summary>Assists.</summary>
    public int Assists { get; private set; }

    /// <summary>Steals.</summary>
    public int Steals { get; private set; }

    /// <summary>Blocks.</summary>
    public int Blocks { get; private set; }

    /// <summary>Turnovers.</summary>
    public int Turnovers { get; private set; }

    /// <summary>Fouls committed.</summary>
    public int FoulsCommitted { get; private set; }

    /// <summary>Seconds played.</summary>
    public int SecondsPlayed { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Total rebounds.</summary>
    public int TotalRebounds => OffensiveRebounds + DefensiveRebounds;

    /// <summary>Builds career totals from every statline a player has, in any competition.</summary>
    public static PlayerCareerAggregate FromStatlines(PlayerId playerId, IReadOnlyList<PlayerGameStatline> lines)
        => new()
        {
            Id = PlayerCareerAggregateId.New(),
            PlayerId = playerId,
            GamesPlayed = lines.Count,
            CompetitionsPlayed = lines.Select(l => l.CompetitionId).Distinct().Count(),
            Points = lines.Sum(l => l.Points),
            FieldGoalsMade = lines.Sum(l => l.FieldGoalsMade),
            FieldGoalsAttempted = lines.Sum(l => l.FieldGoalsAttempted),
            ThreePointersMade = lines.Sum(l => l.ThreePointersMade),
            ThreePointersAttempted = lines.Sum(l => l.ThreePointersAttempted),
            FreeThrowsMade = lines.Sum(l => l.FreeThrowsMade),
            FreeThrowsAttempted = lines.Sum(l => l.FreeThrowsAttempted),
            OffensiveRebounds = lines.Sum(l => l.OffensiveRebounds),
            DefensiveRebounds = lines.Sum(l => l.DefensiveRebounds),
            Assists = lines.Sum(l => l.Assists),
            Steals = lines.Sum(l => l.Steals),
            Blocks = lines.Sum(l => l.Blocks),
            Turnovers = lines.Sum(l => l.Turnovers),
            FoulsCommitted = lines.Sum(l => l.FoulsCommitted),
            SecondsPlayed = lines.Sum(l => l.SecondsPlayed),
        };
}

/// <summary>A team's row in a competition's standings table.</summary>
public sealed class CompetitionStanding : ITenantScoped, IAuditableEntity
{
    private CompetitionStanding()
    {
    }

    /// <summary>Primary key.</summary>
    public CompetitionStandingId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The competition.</summary>
    public CompetitionId CompetitionId { get; private set; }

    /// <summary>The group, when the competition has pools.</summary>
    public GroupId? GroupId { get; private set; }

    /// <summary>The team.</summary>
    public CompetitionTeamId CompetitionTeamId { get; private set; }

    /// <summary>Games played.</summary>
    public int Played { get; private set; }

    /// <summary>Wins.</summary>
    public int Won { get; private set; }

    /// <summary>Losses.</summary>
    public int Lost { get; private set; }

    /// <summary>Draws (only where the rule set allows ties).</summary>
    public int Drawn { get; private set; }

    /// <summary>Points scored.</summary>
    public int PointsFor { get; private set; }

    /// <summary>Points conceded.</summary>
    public int PointsAgainst { get; private set; }

    /// <summary>League points, per the rule set's win/loss/draw values.</summary>
    public int LeaguePoints { get; private set; }

    /// <summary>Final position after tiebreakers, 1-based.</summary>
    public int Position { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Point differential.</summary>
    public int PointDifferential => PointsFor - PointsAgainst;

    /// <summary>Builds a standings row.</summary>
    public static CompetitionStanding Create(
        OrganisationId organisationId, CompetitionId competitionId, GroupId? groupId,
        CompetitionTeamId teamId, int played, int won, int lost, int drawn,
        int pointsFor, int pointsAgainst, int leaguePoints)
        => new()
        {
            Id = CompetitionStandingId.New(),
            OrganisationId = organisationId,
            CompetitionId = competitionId,
            GroupId = groupId,
            CompetitionTeamId = teamId,
            Played = played,
            Won = won,
            Lost = lost,
            Drawn = drawn,
            PointsFor = pointsFor,
            PointsAgainst = pointsAgainst,
            LeaguePoints = leaguePoints,
        };

    /// <summary>Assigns the final table position.</summary>
    public void SetPosition(int position) => Position = position;
}
