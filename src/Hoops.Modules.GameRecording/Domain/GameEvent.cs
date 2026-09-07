using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.GameRecording.Domain;

/// <summary>
/// One append-only entry in a game's event log — the source of truth for everything derived (ADR-001).
/// Rows are NEVER updated (except <see cref="IsVoided"/>) and NEVER deleted. Corrections are made by
/// appending a VOID event, which flags the target rather than erasing it.
/// </summary>
public sealed class GameEvent : ITenantScoped
{
    private GameEvent()
    {
        EventType = null!;
        Payload = new Dictionary<string, string>();
    }

    private GameEvent(
        Guid id, OrganisationId organisationId, GameId gameId, long sequence, string eventType,
        int period, int gameClockMs, DateTimeOffset recordedAt, UserId recordedByUserId)
    {
        Id = id;
        OrganisationId = organisationId;
        GameId = gameId;
        Sequence = sequence;
        EventType = eventType;
        Period = period;
        GameClockMs = gameClockMs;
        RecordedAt = recordedAt;
        RecordedByUserId = recordedByUserId;
        Payload = new Dictionary<string, string>();
    }

    /// <summary>The CLIENT-generated id (UUID v7). Doubles as the idempotency key (ADR-007).</summary>
    public Guid Id { get; private set; }

    /// <summary>The owning organisation. Tenant-scoped like the game.</summary>
    public OrganisationId OrganisationId { get; private set; }

    /// <summary>The game this event belongs to.</summary>
    public GameId GameId { get; private set; }

    /// <summary>Monotonic position within the game's log, starting at 1.</summary>
    public long Sequence { get; private set; }

    /// <summary>The event type (§7).</summary>
    public string EventType { get; private set; }

    /// <summary>The event subtype, where the type has one.</summary>
    public string? EventSubtype { get; private set; }

    /// <summary>The period this event occurred in (1-based).</summary>
    public int Period { get; private set; }

    /// <summary>Game clock in milliseconds REMAINING in the period.</summary>
    public int GameClockMs { get; private set; }

    /// <summary>Shot clock in milliseconds remaining, when known.</summary>
    public int? ShotClockMs { get; private set; }

    /// <summary>The acting team, for team-attributed events.</summary>
    public CompetitionTeamId? CompetitionTeamId { get; private set; }

    /// <summary>The primary actor, as a game-roster entry (the frozen snapshot row).</summary>
    public GameRosterEntryId? GameRosterEntryId { get; private set; }

    /// <summary>The secondary participant: assister, blocker, stealer, player fouled, or player in.</summary>
    public GameRosterEntryId? SecondaryRosterEntryId { get; private set; }

    /// <summary>Points scored by this event (2, 3, or 1 for a free throw).</summary>
    public int? Points { get; private set; }

    /// <summary>Shot x in the canonical frame (§8).</summary>
    public int? ShotXCm { get; private set; }

    /// <summary>Shot y in the canonical frame (§8).</summary>
    public int? ShotYCm { get; private set; }

    /// <summary>Server-computed shot zone. Never trusted from the client.</summary>
    public ShotZone? ShotZone { get; private set; }

    /// <summary>Server-computed distance from the hoop, in cm.</summary>
    public int? ShotDistanceCm { get; private set; }

    /// <summary>Type-specific extras (shot type, free-throw sequence, void reason, overrides…).</summary>
    public Dictionary<string, string> Payload { get; private set; }

    /// <summary>Whether a later VOID event has cancelled this one. The ONLY mutable field.</summary>
    public bool IsVoided { get; private set; }

    /// <summary>When the client recorded the event (device clock; survives a sync gap).</summary>
    public DateTimeOffset? ClientRecordedAt { get; private set; }

    /// <summary>When the server accepted the event.</summary>
    public DateTimeOffset RecordedAt { get; private set; }

    /// <summary>Who recorded it.</summary>
    public UserId RecordedByUserId { get; private set; }

    /// <summary>Records a new event at <paramref name="sequence"/>.</summary>
    public static GameEvent Record(
        Guid eventId,
        OrganisationId organisationId,
        GameId gameId,
        long sequence,
        string eventType,
        string? eventSubtype,
        int period,
        int gameClockMs,
        DateTimeOffset recordedAt,
        UserId recordedByUserId,
        int? shotClockMs = null,
        CompetitionTeamId? competitionTeamId = null,
        GameRosterEntryId? gameRosterEntryId = null,
        GameRosterEntryId? secondaryRosterEntryId = null,
        int? points = null,
        int? shotXCm = null,
        int? shotYCm = null,
        DateTimeOffset? clientRecordedAt = null,
        IReadOnlyDictionary<string, string>? payload = null)
    {
        Guard.AgainstNullOrWhiteSpace(eventType);

        var e = new GameEvent(eventId, organisationId, gameId, sequence, eventType, period, gameClockMs, recordedAt, recordedByUserId)
        {
            EventSubtype = eventSubtype,
            ShotClockMs = shotClockMs,
            CompetitionTeamId = competitionTeamId,
            GameRosterEntryId = gameRosterEntryId,
            SecondaryRosterEntryId = secondaryRosterEntryId,
            Points = points,
            ShotXCm = shotXCm,
            ShotYCm = shotYCm,
            ClientRecordedAt = clientRecordedAt,
        };

        if (payload is not null)
        {
            e.Payload = new Dictionary<string, string>(payload);
        }

        // Points are derived from the event type and subtype rather than trusted from the client, for
        // the same reason the server recomputes shot zone and distance: a client-supplied value that
        // disagrees with the event would corrupt every derived statistic downstream.
        e.Points = eventType switch
        {
            EventTypes.FieldGoalMade => string.Equals(eventSubtype, EventSubtypes.ThreePoint, StringComparison.Ordinal) ? 3 : 2,
            EventTypes.FreeThrowMade => 1,
            _ => points,
        };

        // The server always recomputes zone and distance from the coordinates (§8).
        if (shotXCm.HasValue && shotYCm.HasValue)
        {
            e.ShotZone = CourtGeometry.Classify(shotXCm.Value, shotYCm.Value);
            e.ShotDistanceCm = (int)Math.Round(CourtGeometry.DistanceFromHoop(shotXCm.Value, shotYCm.Value));
        }

        return e;
    }

    /// <summary>Marks this event voided. The row itself is retained forever.</summary>
    public void Void() => IsVoided = true;

    /// <summary>True when a tier-2 rule was overridden to record this event.</summary>
    public bool WasOverridden => Payload.ContainsKey("overridden");

    /// <summary>
    /// Flags this event as recorded over a tier-2 violation, with the rule code and the caller's
    /// reason, so the review screen can surface it prominently (§10).
    /// </summary>
    public void MarkOverridden(string ruleCode, string? reason)
    {
        Payload["overridden"] = ruleCode;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Payload["overrideReason"] = reason;
        }
    }
}
