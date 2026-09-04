using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.GameRecording.Domain;

/// <summary>
/// A frozen roster snapshot row for one game, written at roster lock. It copies the player, jersey,
/// position and captaincy AS THEY WERE at lock time; later edits to <c>roster_entries</c> never touch
/// this snapshot. That immutability is what makes recorded history stable.
/// </summary>
public sealed class GameRosterEntry : ITenantScoped, IAuditableEntity
{
    private GameRosterEntry()
    {
        JerseyNumber = null!;
    }

    private GameRosterEntry(
        GameRosterEntryId id, OrganisationId organisationId, GameId gameId, CompetitionTeamId competitionTeamId,
        PlayerId playerId, string jerseyNumber, string? position, bool isStarter, bool isCaptain)
    {
        Id = id;
        OrganisationId = organisationId;
        GameId = gameId;
        CompetitionTeamId = competitionTeamId;
        PlayerId = playerId;
        JerseyNumber = jerseyNumber;
        Position = position;
        IsStarter = isStarter;
        IsCaptain = isCaptain;
    }

    /// <summary>Primary key.</summary>
    public GameRosterEntryId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The game this snapshot belongs to.</summary>
    public GameId GameId { get; private set; }

    /// <summary>The team.</summary>
    public CompetitionTeamId CompetitionTeamId { get; private set; }

    /// <summary>The player (registry id).</summary>
    public PlayerId PlayerId { get; private set; }

    /// <summary>Jersey number, frozen at lock time.</summary>
    public string JerseyNumber { get; private set; }

    /// <summary>Position, frozen at lock time.</summary>
    public string? Position { get; private set; }

    /// <summary>Whether the player was a starter.</summary>
    public bool IsStarter { get; private set; }

    /// <summary>Whether the player was captain.</summary>
    public bool IsCaptain { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Captures a snapshot row.</summary>
    public static GameRosterEntry Capture(
        OrganisationId organisationId, GameId gameId, CompetitionTeamId competitionTeamId, PlayerId playerId,
        string jerseyNumber, string? position, bool isStarter, bool isCaptain)
    {
        Guard.AgainstNullOrWhiteSpace(jerseyNumber);
        return new GameRosterEntry(GameRosterEntryId.New(), organisationId, gameId, competitionTeamId, playerId,
            jerseyNumber, position, isStarter, isCaptain);
    }
}

/// <summary>An official assigned to a game (§7 needs coach/official records for technical fouls).</summary>
public sealed class GameOfficial : ITenantScoped, IAuditableEntity
{
    private GameOfficial()
    {
        FullName = null!;
    }

    private GameOfficial(GameOfficialId id, OrganisationId organisationId, GameId gameId, string fullName, OfficialRole role)
    {
        Id = id;
        OrganisationId = organisationId;
        GameId = gameId;
        FullName = fullName;
        Role = role;
    }

    /// <summary>Primary key.</summary>
    public GameOfficialId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The game.</summary>
    public GameId GameId { get; private set; }

    /// <summary>The official's name.</summary>
    public string FullName { get; private set; }

    /// <summary>Their role.</summary>
    public OfficialRole Role { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Assigns an official to a game.</summary>
    public static GameOfficial Assign(OrganisationId organisationId, GameId gameId, string fullName, OfficialRole role)
    {
        Guard.AgainstNullOrWhiteSpace(fullName);
        return new GameOfficial(GameOfficialId.New(), organisationId, gameId, fullName.Trim(), role);
    }
}
