using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Statistics.Domain;

/// <summary>
/// A player's persisted statline for one finalised game. Written ONLY by the projector on
/// finalisation and fully rebuildable from <c>game_events</c> — never authored, never patched.
/// </summary>
public sealed class PlayerGameStatline : ITenantScoped, IAuditableEntity
{
    private PlayerGameStatline()
    {
    }

    /// <summary>Primary key.</summary>
    public PlayerGameStatlineId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The game.</summary>
    public GameId GameId { get; private set; }

    /// <summary>The competition, denormalised so aggregates need no join back through games.</summary>
    public CompetitionId CompetitionId { get; private set; }

    /// <summary>The team.</summary>
    public CompetitionTeamId CompetitionTeamId { get; private set; }

    /// <summary>The registry player.</summary>
    public PlayerId PlayerId { get; private set; }

    /// <summary>The frozen roster row this line came from.</summary>
    public GameRosterEntryId GameRosterEntryId { get; private set; }

    /// <summary>Points scored.</summary>
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

    /// <summary>Shots of this player's blocked by an opponent.</summary>
    public int BlocksAgainst { get; private set; }

    /// <summary>Turnovers.</summary>
    public int Turnovers { get; private set; }

    /// <summary>Fouls committed.</summary>
    public int FoulsCommitted { get; private set; }

    /// <summary>Fouls drawn.</summary>
    public int FoulsDrawn { get; private set; }

    /// <summary>Whether the player fouled out.</summary>
    public bool FouledOut { get; private set; }

    /// <summary>Seconds played.</summary>
    public int SecondsPlayed { get; private set; }

    /// <summary>Plus/minus.</summary>
    public int PlusMinus { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Total rebounds.</summary>
    public int TotalRebounds => OffensiveRebounds + DefensiveRebounds;

    /// <summary>Persists a projected statline.</summary>
    public static PlayerGameStatline FromProjection(
        OrganisationId organisationId, GameId gameId, CompetitionId competitionId,
        CompetitionTeamId competitionTeamId, PlayerId playerId, GameRosterEntryId gameRosterEntryId,
        int points, int fgm, int fga, int tpm, int tpa, int ftm, int fta, int oreb, int dreb,
        int assists, int steals, int blocks, int blocksAgainst, int turnovers, int foulsCommitted,
        int foulsDrawn, bool fouledOut, int secondsPlayed, int plusMinus)
        => new()
        {
            Id = PlayerGameStatlineId.New(),
            OrganisationId = organisationId,
            GameId = gameId,
            CompetitionId = competitionId,
            CompetitionTeamId = competitionTeamId,
            PlayerId = playerId,
            GameRosterEntryId = gameRosterEntryId,
            Points = points,
            FieldGoalsMade = fgm,
            FieldGoalsAttempted = fga,
            ThreePointersMade = tpm,
            ThreePointersAttempted = tpa,
            FreeThrowsMade = ftm,
            FreeThrowsAttempted = fta,
            OffensiveRebounds = oreb,
            DefensiveRebounds = dreb,
            Assists = assists,
            Steals = steals,
            Blocks = blocks,
            BlocksAgainst = blocksAgainst,
            Turnovers = turnovers,
            FoulsCommitted = foulsCommitted,
            FoulsDrawn = foulsDrawn,
            FouledOut = fouledOut,
            SecondsPlayed = secondsPlayed,
            PlusMinus = plusMinus,
        };
}

/// <summary>A team's persisted statline for one finalised game.</summary>
public sealed class TeamGameStatline : ITenantScoped, IAuditableEntity
{
    private TeamGameStatline()
    {
    }

    /// <summary>Primary key.</summary>
    public TeamGameStatlineId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The game.</summary>
    public GameId GameId { get; private set; }

    /// <summary>The competition.</summary>
    public CompetitionId CompetitionId { get; private set; }

    /// <summary>The team.</summary>
    public CompetitionTeamId CompetitionTeamId { get; private set; }

    /// <summary>The opponent.</summary>
    public CompetitionTeamId OpponentCompetitionTeamId { get; private set; }

    /// <summary>Whether this team was at home.</summary>
    public bool IsHome { get; private set; }

    /// <summary>Points scored.</summary>
    public int Points { get; private set; }

    /// <summary>Points conceded.</summary>
    public int PointsAgainst { get; private set; }

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

    /// <summary>Whether this team won.</summary>
    public bool Won { get; private set; }

    /// <summary>Whether the game was drawn.</summary>
    public bool Drawn { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Persists a projected team statline.</summary>
    public static TeamGameStatline FromProjection(
        OrganisationId organisationId, GameId gameId, CompetitionId competitionId,
        CompetitionTeamId competitionTeamId, CompetitionTeamId opponentId, bool isHome,
        int points, int pointsAgainst, int fgm, int fga, int tpm, int tpa, int ftm, int fta,
        int oreb, int dreb, int assists, int steals, int blocks, int turnovers, int foulsCommitted)
        => new()
        {
            Id = TeamGameStatlineId.New(),
            OrganisationId = organisationId,
            GameId = gameId,
            CompetitionId = competitionId,
            CompetitionTeamId = competitionTeamId,
            OpponentCompetitionTeamId = opponentId,
            IsHome = isHome,
            Points = points,
            PointsAgainst = pointsAgainst,
            FieldGoalsMade = fgm,
            FieldGoalsAttempted = fga,
            ThreePointersMade = tpm,
            ThreePointersAttempted = tpa,
            FreeThrowsMade = ftm,
            FreeThrowsAttempted = fta,
            OffensiveRebounds = oreb,
            DefensiveRebounds = dreb,
            Assists = assists,
            Steals = steals,
            Blocks = blocks,
            Turnovers = turnovers,
            FoulsCommitted = foulsCommitted,
            Won = points > pointsAgainst,
            Drawn = points == pointsAgainst,
        };
}

/// <summary>A period's persisted scoring line for one finalised game.</summary>
public sealed class GamePeriodStateRow : ITenantScoped, IAuditableEntity
{
    private GamePeriodStateRow()
    {
    }

    /// <summary>Primary key.</summary>
    public GamePeriodStateId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The game.</summary>
    public GameId GameId { get; private set; }

    /// <summary>The team.</summary>
    public CompetitionTeamId CompetitionTeamId { get; private set; }

    /// <summary>The period number.</summary>
    public int Period { get; private set; }

    /// <summary>Points scored by this team in this period.</summary>
    public int Points { get; private set; }

    /// <summary>Team fouls in this period.</summary>
    public int TeamFouls { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Persists a projected period state.</summary>
    public static GamePeriodStateRow Create(
        OrganisationId organisationId, GameId gameId, CompetitionTeamId teamId, int period, int points, int teamFouls)
        => new()
        {
            Id = GamePeriodStateId.New(),
            OrganisationId = organisationId,
            GameId = gameId,
            CompetitionTeamId = teamId,
            Period = period,
            Points = points,
            TeamFouls = teamFouls,
        };
}

/// <summary>A persisted lineup stint, derived from substitutions.</summary>
public sealed class LineupStintRow : ITenantScoped, IAuditableEntity
{
    private LineupStintRow()
    {
        PlayerIds = null!;
    }

    /// <summary>Primary key.</summary>
    public LineupStintId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The game.</summary>
    public GameId GameId { get; private set; }

    /// <summary>The team.</summary>
    public CompetitionTeamId CompetitionTeamId { get; private set; }

    /// <summary>The period.</summary>
    public int Period { get; private set; }

    /// <summary>Sorted, comma-joined game-roster ids — the lineup's stable identity.</summary>
    public string PlayerIds { get; private set; }

    /// <summary>Seconds this lineup was on court with the clock running.</summary>
    public int SecondsPlayed { get; private set; }

    /// <summary>Points scored while this lineup was on.</summary>
    public int PointsFor { get; private set; }

    /// <summary>Points conceded while this lineup was on.</summary>
    public int PointsAgainst { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Net points for this lineup.</summary>
    public int PlusMinus => PointsFor - PointsAgainst;

    /// <summary>Persists a projected stint.</summary>
    public static LineupStintRow Create(
        OrganisationId organisationId, GameId gameId, CompetitionTeamId teamId, int period,
        string playerIds, int secondsPlayed, int pointsFor, int pointsAgainst)
        => new()
        {
            Id = LineupStintId.New(),
            OrganisationId = organisationId,
            GameId = gameId,
            CompetitionTeamId = teamId,
            Period = period,
            PlayerIds = playerIds,
            SecondsPlayed = secondsPlayed,
            PointsFor = pointsFor,
            PointsAgainst = pointsAgainst,
        };
}
