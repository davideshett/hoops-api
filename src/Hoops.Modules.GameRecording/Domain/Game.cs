using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.GameRecording.Domain;

/// <summary>
/// A fixture between two competition teams. Progresses through the §6 state machine. At roster lock a
/// <see cref="RuleSetSnapshot"/> is frozen from the competition's current rules, so later edits to the
/// competition rule set never change a locked game (that immutability is the point of Phase 3).
/// </summary>
public sealed class Game : ITenantScoped, IAuditableEntity
{
    private Game()
    {
    }

    private Game(
        GameId id, OrganisationId organisationId, CompetitionId competitionId,
        CompetitionTeamId homeCompetitionTeamId, CompetitionTeamId awayCompetitionTeamId, DateTimeOffset scheduledAt)
    {
        Id = id;
        OrganisationId = organisationId;
        CompetitionId = competitionId;
        HomeCompetitionTeamId = homeCompetitionTeamId;
        AwayCompetitionTeamId = awayCompetitionTeamId;
        ScheduledAt = scheduledAt;
    }

    /// <summary>Primary key.</summary>
    public GameId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The competition this game belongs to.</summary>
    public CompetitionId CompetitionId { get; private set; }

    /// <summary>The stage, if assigned.</summary>
    public StageId? StageId { get; private set; }

    /// <summary>The group, if assigned.</summary>
    public GroupId? GroupId { get; private set; }

    /// <summary>The home team's competition entry.</summary>
    public CompetitionTeamId HomeCompetitionTeamId { get; private set; }

    /// <summary>The away team's competition entry.</summary>
    public CompetitionTeamId AwayCompetitionTeamId { get; private set; }

    /// <summary>The venue, if assigned.</summary>
    public VenueId? VenueId { get; private set; }

    /// <summary>Tip-off time, UTC.</summary>
    public DateTimeOffset ScheduledAt { get; private set; }

    /// <summary>Current status.</summary>
    public GameStatus Status { get; private set; } = GameStatus.Scheduled;

    /// <summary>When play started, once it has (Phase 4).</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>The rules frozen onto this game at roster lock. Null until locked.</summary>
    public RuleSet? RuleSetSnapshot { get; private set; }

    /// <summary>When the roster was locked, UTC.</summary>
    public DateTimeOffset? RosterLockedAt { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Schedules a new fixture. A team cannot be scheduled against itself.</summary>
    public static Game Schedule(
        OrganisationId organisationId, CompetitionId competitionId,
        CompetitionTeamId homeCompetitionTeamId, CompetitionTeamId awayCompetitionTeamId, DateTimeOffset scheduledAt,
        StageId? stageId = null, GroupId? groupId = null, VenueId? venueId = null)
    {
        if (homeCompetitionTeamId == awayCompetitionTeamId)
        {
            throw new ArgumentException("A team cannot be scheduled against itself.");
        }

        return new Game(GameId.New(), organisationId, competitionId, homeCompetitionTeamId, awayCompetitionTeamId, scheduledAt)
        {
            StageId = stageId,
            GroupId = groupId,
            VenueId = venueId,
        };
    }

    /// <summary>True while the fixture can still be edited or generated over.</summary>
    public bool IsScheduled => Status is GameStatus.Scheduled or GameStatus.Postponed;

    /// <summary>Reschedules a fixture (time/venue). Only allowed before roster lock.</summary>
    public bool Reschedule(DateTimeOffset? scheduledAt, VenueId? venueId, bool clearVenue)
    {
        if (!IsScheduled)
        {
            return false;
        }

        if (scheduledAt.HasValue)
        {
            ScheduledAt = scheduledAt.Value;
        }

        if (clearVenue)
        {
            VenueId = null;
        }
        else if (venueId.HasValue)
        {
            VenueId = venueId;
        }

        if (Status == GameStatus.Postponed)
        {
            Status = GameStatus.Scheduled;
        }

        return true;
    }

    /// <summary>Assigns the stage/group this game belongs to.</summary>
    public void AssignBracket(StageId? stageId, GroupId? groupId)
    {
        StageId = stageId;
        GroupId = groupId;
    }

    /// <summary>
    /// Locks the roster: freezes <paramref name="ruleSet"/> onto the game and transitions to
    /// RosterLocked. The caller writes the game_roster_entries snapshot in the same transaction.
    /// </summary>
    public bool LockRoster(RuleSet ruleSet, DateTimeOffset at)
    {
        if (Status != GameStatus.Scheduled)
        {
            return false;
        }

        RuleSetSnapshot = ruleSet;
        RosterLockedAt = at;
        Status = GameStatus.RosterLocked;
        return true;
    }

    /// <summary>Postpones a scheduled game.</summary>
    public bool Postpone()
    {
        if (Status != GameStatus.Scheduled)
        {
            return false;
        }

        Status = GameStatus.Postponed;
        return true;
    }

    /// <summary>Cancels a scheduled or postponed game.</summary>
    public bool Cancel()
    {
        if (!IsScheduled)
        {
            return false;
        }

        Status = GameStatus.Cancelled;
        return true;
    }
}
