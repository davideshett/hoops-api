using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Registry.Domain;

/// <summary>
/// A player's participation in one competition-team's squad, with a jersey number (§5A.1). Built ONLY
/// from an existing <see cref="PlayerId"/> resolved from the registry — never from a free-text name.
/// This is tenant-owned (org-scoped) data. Jersey numbers are strings: "00" and "0" are distinct.
/// </summary>
public sealed class RosterEntry : ITenantScoped, IAuditableEntity
{
    private RosterEntry()
    {
        JerseyNumber = null!;
    }

    private RosterEntry(
        RosterEntryId id, OrganisationId organisationId, CompetitionTeamId competitionTeamId, PlayerId playerId,
        int verifiedTier, string jerseyNumber, DateTimeOffset registeredAt)
    {
        Id = id;
        OrganisationId = organisationId;
        CompetitionTeamId = competitionTeamId;
        PlayerId = playerId;
        VerifiedTier = verifiedTier;
        JerseyNumber = jerseyNumber;
        RegisteredAt = registeredAt;
    }

    /// <summary>Primary key.</summary>
    public RosterEntryId Id { get; private set; }

    /// <inheritdoc />
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The competition-team squad this entry belongs to.</summary>
    public CompetitionTeamId CompetitionTeamId { get; private set; }

    /// <summary>The registry player. Resolved from the registry, never typed.</summary>
    public PlayerId PlayerId { get; private set; }

    /// <summary>The player's identity tier at the moment of registration (a snapshot).</summary>
    public int VerifiedTier { get; private set; }

    /// <summary>Jersey number, as text ("00" and "0" are different players in FIBA).</summary>
    public string JerseyNumber { get; private set; }

    /// <summary>Playing position, if recorded (PG/SG/SF/PF/C).</summary>
    public string? Position { get; private set; }

    /// <summary>Whether this player is the team captain.</summary>
    public bool IsCaptain { get; private set; }

    /// <summary>Roster status.</summary>
    public RosterEntryStatus Status { get; private set; } = RosterEntryStatus.Active;

    /// <summary>When the player was added to the squad.</summary>
    public DateTimeOffset RegisteredAt { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Adds a player (by id) to a squad with a jersey number.</summary>
    public static RosterEntry Register(
        OrganisationId organisationId, CompetitionTeamId competitionTeamId, PlayerId playerId,
        int verifiedTier, string jerseyNumber, DateTimeOffset registeredAt, string? position, bool isCaptain)
    {
        Guard.AgainstNullOrWhiteSpace(jerseyNumber);
        return new RosterEntry(RosterEntryId.New(), organisationId, competitionTeamId, playerId, verifiedTier, jerseyNumber.Trim(), registeredAt)
        {
            Position = string.IsNullOrWhiteSpace(position) ? null : position.Trim(),
            IsCaptain = isCaptain,
        };
    }

    /// <summary>Repoints this entry to a surviving player (merge).</summary>
    public void RepointTo(PlayerId survivor) => PlayerId = survivor;

    /// <summary>Updates mutable fields; omitted arguments unchanged.</summary>
    public void Update(string? jerseyNumber, string? position, bool? isCaptain, RosterEntryStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(jerseyNumber))
        {
            JerseyNumber = jerseyNumber.Trim();
        }

        if (position is not null)
        {
            Position = string.IsNullOrWhiteSpace(position) ? null : position.Trim();
        }

        if (isCaptain.HasValue)
        {
            IsCaptain = isCaptain.Value;
        }

        if (status.HasValue)
        {
            Status = status.Value;
        }
    }
}
