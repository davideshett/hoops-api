using Hoops.SharedKernel;
using Hoops.Modules.GameRecording.Domain.Projection;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.GameRecording.Domain.Validation;

/// <summary>How serious a rule violation is (§10).</summary>
public enum RuleTier
{
    /// <summary>Certainly a data-entry error. Never overridable.</summary>
    Reject = 1,

    /// <summary>Probably an error, but might be reality. Overridable with a reason.</summary>
    Overridable = 2,
}

/// <summary>The outcome of evaluating one rule.</summary>
/// <param name="IsValid">True when the rule permits the event.</param>
/// <param name="Code">The machine-readable rule code, when it does not.</param>
/// <param name="Message">A human-facing explanation.</param>
/// <param name="Tier">Whether the failure can be overridden.</param>
public sealed record RuleResult(bool IsValid, string? Code = null, string? Message = null, RuleTier Tier = RuleTier.Reject)
{
    /// <summary>A passing result.</summary>
    public static readonly RuleResult Valid = new(true);

    /// <summary>A tier-1 failure: never overridable.</summary>
    public static RuleResult Reject(string code, string message) => new(false, code, message, RuleTier.Reject);

    /// <summary>A tier-2 failure: overridable by a CompetitionManager with a reason.</summary>
    public static RuleResult Overridable(string code, string message) => new(false, code, message, RuleTier.Overridable);
}

/// <summary>Everything a rule needs to judge a candidate event, without touching persistence.</summary>
/// <param name="Game">The frozen game context (teams, roster, rules).</param>
/// <param name="State">The live state derived from the log so far.</param>
/// <param name="Status">The game's current status.</param>
/// <param name="PreviousEvent">The most recent non-voided event, or null.</param>
/// <param name="FreeThrowsAwarded">Free throws still owed from the most recent shooting foul.</param>
public sealed record ValidationContext(
    GameContext Game,
    LiveGameState State,
    GameStatus Status,
    GameEvent? PreviousEvent,
    int FreeThrowsAwarded);

/// <summary>One rule in the ordered validation pipeline (§10).</summary>
public interface IEventRule
{
    /// <summary>The code this rule reports on failure.</summary>
    string Code { get; }

    /// <summary>Judges a candidate event.</summary>
    RuleResult Evaluate(ValidationContext context, GameEvent candidate);
}

/// <summary>Shared helpers for rules.</summary>
internal static class RuleHelpers
{
    public static GamePlayer? Find(this GameContext game, GameRosterEntryId? id)
        => id.HasValue ? game.Players.FirstOrDefault(p => p.GameRosterEntryId == id.Value) : null;

    public static bool IsPlayEvent(string type) => type is
        EventTypes.FieldGoalMade or EventTypes.FieldGoalMissed or EventTypes.FreeThrowMade or
        EventTypes.FreeThrowMissed or EventTypes.Rebound or EventTypes.Turnover or EventTypes.Foul;

    public static bool IsSetupEvent(string type) => type is
        EventTypes.GameStart or EventTypes.PeriodStart or EventTypes.Note or EventTypes.Void;

    public static bool IsShot(string type) => type is EventTypes.FieldGoalMade or EventTypes.FieldGoalMissed;
}

// ══ Tier 1 — reject ═══════════════════════════════════════════════════════════

/// <summary>The game must be in progress for anything but GAME_START.</summary>
public sealed class GameInProgressRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "GAME_NOT_IN_PROGRESS";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.EventType == EventTypes.GameStart)
        {
            return c.Status is GameStatus.RosterLocked or GameStatus.InProgress
                ? RuleResult.Valid
                : RuleResult.Reject(Code, "A game must have a locked roster before it can start.");
        }

        return c.Status == GameStatus.InProgress
            ? RuleResult.Valid
            : RuleResult.Reject(Code, "The game is not in progress.");
    }
}

/// <summary>No play event may precede GAME_START.</summary>
public sealed class EventBeforeGameStartRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "EVENT_BEFORE_GAME_START";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
        => RuleHelpers.IsSetupEvent(e.EventType) || c.State.GameStarted
            ? RuleResult.Valid
            : RuleResult.Reject(Code, "This event cannot precede GAME_START.");
}

/// <summary>Every referenced roster entry must belong to this game.</summary>
public sealed class PlayerOnRosterRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "PLAYER_NOT_ON_ROSTER";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        foreach (var id in new[] { e.GameRosterEntryId, e.SecondaryRosterEntryId })
        {
            if (id.HasValue && c.Game.Find(id) is null)
            {
                return RuleResult.Reject(Code, "That player is not on this game's roster.");
            }
        }

        return RuleResult.Valid;
    }
}

/// <summary>The actor's team must match the event's stated team.</summary>
public sealed class WrongTeamRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "WRONG_TEAM";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.CompetitionTeamId is not { } team || c.Game.Find(e.GameRosterEntryId) is not { } actor)
        {
            return RuleResult.Valid;
        }

        return actor.CompetitionTeamId == team
            ? RuleResult.Valid
            : RuleResult.Reject(Code, "The player does not play for the stated team.");
    }
}

/// <summary>Shot coordinates must be on the court.</summary>
public sealed class ShotInBoundsRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "SHOT_OUT_OF_BOUNDS";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.ShotXCm is not { } x || e.ShotYCm is not { } y)
        {
            return RuleResult.Valid;
        }

        return CourtGeometry.IsInBounds(x, y)
            ? RuleResult.Valid
            : RuleResult.Reject(Code, "Shot coordinates are outside the court.");
    }
}

/// <summary>A player cannot assist their own basket.</summary>
public sealed class AssistSelfRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "ASSIST_SELF";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
        => e.EventType == EventTypes.FieldGoalMade
           && e.SecondaryRosterEntryId.HasValue
           && e.SecondaryRosterEntryId == e.GameRosterEntryId
            ? RuleResult.Reject(Code, "A player cannot assist their own basket.")
            : RuleResult.Valid;
}

/// <summary>An assister must be a teammate of the scorer.</summary>
public sealed class AssistTeammateRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "ASSIST_WRONG_TEAM";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
        => e.EventType == EventTypes.FieldGoalMade
           && SameTeam(c, e) == false
            ? RuleResult.Reject(Code, "The assisting player must be a teammate.")
            : RuleResult.Valid;

    private static bool? SameTeam(ValidationContext c, GameEvent e)
    {
        var shooter = c.Game.Find(e.GameRosterEntryId);
        var assister = c.Game.Find(e.SecondaryRosterEntryId);
        return shooter is null || assister is null ? null : shooter.CompetitionTeamId == assister.CompetitionTeamId;
    }
}

/// <summary>A stealer must be an opponent of the player who lost the ball.</summary>
public sealed class StealOpponentRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "STEAL_SAME_TEAM";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.EventType != EventTypes.Turnover)
        {
            return RuleResult.Valid;
        }

        var committer = c.Game.Find(e.GameRosterEntryId);
        var stealer = c.Game.Find(e.SecondaryRosterEntryId);
        return committer is not null && stealer is not null && committer.CompetitionTeamId == stealer.CompetitionTeamId
            ? RuleResult.Reject(Code, "The stealing player must be an opponent.")
            : RuleResult.Valid;
    }
}

/// <summary>A blocker must be an opponent of the shooter.</summary>
public sealed class BlockOpponentRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "BLOCK_SAME_TEAM";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.EventType != EventTypes.FieldGoalMissed)
        {
            return RuleResult.Valid;
        }

        var shooter = c.Game.Find(e.GameRosterEntryId);
        var blocker = c.Game.Find(e.SecondaryRosterEntryId);
        return shooter is not null && blocker is not null && shooter.CompetitionTeamId == blocker.CompetitionTeamId
            ? RuleResult.Reject(Code, "The blocking player must be an opponent.")
            : RuleResult.Valid;
    }
}

/// <summary>A fouled player must be an opponent (technicals excepted).</summary>
public sealed class FoulDrawnOpponentRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "FOUL_DRAWN_SAME_TEAM";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.EventType != EventTypes.Foul
            || string.Equals(e.EventSubtype, EventSubtypes.Technical, StringComparison.Ordinal))
        {
            return RuleResult.Valid;
        }

        var offender = c.Game.Find(e.GameRosterEntryId);
        var fouled = c.Game.Find(e.SecondaryRosterEntryId);
        return offender is not null && fouled is not null && offender.CompetitionTeamId == fouled.CompetitionTeamId
            ? RuleResult.Reject(Code, "The fouled player must be an opponent.")
            : RuleResult.Valid;
    }
}

/// <summary>Free-throw attempt numbers must be sequential and within the awarded total.</summary>
public sealed class FreeThrowSequenceRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "FREE_THROW_SEQUENCE";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.EventType is not (EventTypes.FreeThrowMade or EventTypes.FreeThrowMissed))
        {
            return RuleResult.Valid;
        }

        if (!TryGetInt(e, "attemptNumber", out var attempt) || !TryGetInt(e, "totalAttempts", out var total))
        {
            return RuleResult.Valid; // nothing declared to check
        }

        return attempt >= 1 && attempt <= total
            ? RuleResult.Valid
            : RuleResult.Reject(Code, $"Free-throw attempt {attempt} of {total} is out of sequence.");
    }

    private static bool TryGetInt(GameEvent e, string key, out int value)
    {
        value = 0;
        return e.Payload.TryGetValue(key, out var raw) && int.TryParse(raw, out value);
    }
}

/// <summary>A game may not end tied unless the rule set allows it.</summary>
public sealed class TiedAtGameEndRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "TIED_AT_GAME_END";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.EventType != EventTypes.GameEnd || c.Game.RuleSet.AllowsTies)
        {
            return RuleResult.Valid;
        }

        var scores = c.State.Score.Values.ToList();
        return scores.Count == 2 && scores[0] == scores[1]
            ? RuleResult.Reject(Code, "The game cannot end tied; play an overtime period.")
            : RuleResult.Valid;
    }
}

// ══ Tier 2 — overridable ══════════════════════════════════════════════════════

/// <summary>The actor must be on court for play events.</summary>
public sealed class PlayerOnCourtRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "PLAYER_NOT_ON_COURT";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (!RuleHelpers.IsPlayEvent(e.EventType) || c.Game.Find(e.GameRosterEntryId) is not { } actor)
        {
            return RuleResult.Valid;
        }

        var onCourt = c.State.OnCourt.TryGetValue(actor.CompetitionTeamId, out var list) && list.Contains(actor.GameRosterEntryId);
        return onCourt
            ? RuleResult.Valid
            : RuleResult.Overridable(Code, "That player is not on court.");
    }
}

/// <summary>A fouled-out player may record no further events.</summary>
public sealed class PlayerFouledOutRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "PLAYER_FOULED_OUT";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
        => e.GameRosterEntryId.HasValue && c.State.FouledOut.Contains(e.GameRosterEntryId.Value)
            ? RuleResult.Overridable(Code, "That player has fouled out.")
            : RuleResult.Valid;
}

/// <summary>A team must field exactly the configured number of players after a substitution.</summary>
public sealed class LineupSizeRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "INVALID_LINEUP_SIZE";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.EventType != EventTypes.Substitution || c.Game.Find(e.GameRosterEntryId) is not { } outgoing)
        {
            return RuleResult.Valid;
        }

        var incoming = c.Game.Find(e.SecondaryRosterEntryId);
        if (incoming is null || incoming.CompetitionTeamId != outgoing.CompetitionTeamId)
        {
            return RuleResult.Overridable(Code, "A substitution must swap two players from the same team.");
        }

        return RuleResult.Valid;
    }
}

/// <summary>Substitutions require a stopped clock.</summary>
public sealed class SubstitutionWhileLiveRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "SUBSTITUTION_WHILE_LIVE";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
        => e.EventType == EventTypes.Substitution && c.State.ClockRunning
            ? RuleResult.Overridable(Code, "The clock must be stopped to substitute.")
            : RuleResult.Valid;
}

/// <summary>The game clock must not increase within a period.</summary>
public sealed class ClockMonotonicRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "CLOCK_NOT_MONOTONIC";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.EventType == EventTypes.PeriodStart || c.PreviousEvent is not { } previous)
        {
            return RuleResult.Valid;
        }

        return previous.Period == e.Period && e.GameClockMs > previous.GameClockMs
            ? RuleResult.Overridable(Code, "The game clock cannot run backwards within a period.")
            : RuleResult.Valid;
    }
}

/// <summary>The clock must be within the period's duration.</summary>
public sealed class ClockInRangeRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "CLOCK_OUT_OF_RANGE";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.GameClockMs < 0)
        {
            return RuleResult.Overridable(Code, "The game clock cannot be negative.");
        }

        var rules = c.Game.RuleSet;
        var maximum = (e.Period > rules.NumberOfPeriods ? rules.OvertimeDurationSeconds : rules.PeriodDurationSeconds) * 1000;
        return e.GameClockMs > maximum
            ? RuleResult.Overridable(Code, "The game clock exceeds the period duration.")
            : RuleResult.Valid;
    }
}

/// <summary>A rebound must follow a missed shot or a missed final free throw.</summary>
public sealed class ReboundAfterMissRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "REBOUND_WITHOUT_MISS";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.EventType is not (EventTypes.Rebound or EventTypes.TeamRebound))
        {
            return RuleResult.Valid;
        }

        var previous = c.PreviousEvent?.EventType;
        return previous is EventTypes.FieldGoalMissed or EventTypes.FreeThrowMissed
            ? RuleResult.Valid
            : RuleResult.Overridable(Code, "A rebound must follow a missed shot.");
    }
}

/// <summary>A free throw must reference the foul that awarded it.</summary>
public sealed class FreeThrowSourceRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "FREE_THROW_WITHOUT_SOURCE";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.EventType is not (EventTypes.FreeThrowMade or EventTypes.FreeThrowMissed))
        {
            return RuleResult.Valid;
        }

        var hasSource = e.Payload.ContainsKey("sourceEventId") || c.FreeThrowsAwarded > 0;
        return hasSource
            ? RuleResult.Valid
            : RuleResult.Overridable(Code, "A free throw must reference the foul that awarded it.");
    }
}

/// <summary>Shot coordinates must agree with the two/three subtype (§8).</summary>
public sealed class ShotZoneMatchesSubtypeRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "SHOT_ZONE_MISMATCH";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (!RuleHelpers.IsShot(e.EventType) || e.ShotZone is not { } zone || e.EventSubtype is null)
        {
            return RuleResult.Valid;
        }

        var claimsThree = string.Equals(e.EventSubtype, EventSubtypes.ThreePoint, StringComparison.Ordinal);
        var isThreeZone = CourtGeometry.IsThreePointZone(zone);

        return claimsThree == isThreeZone
            ? RuleResult.Valid
            : RuleResult.Overridable(Code,
                $"The shot was recorded as {e.EventSubtype} but its coordinates fall in {zone}.");
    }
}

/// <summary>A team cannot exceed its timeout allowance.</summary>
public sealed class TimeoutLimitRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "TIMEOUT_LIMIT_EXCEEDED";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
    {
        if (e.EventType != EventTypes.Timeout || e.CompetitionTeamId is not { } team)
        {
            return RuleResult.Valid;
        }

        // Media and official timeouts are not charged to a team.
        if (e.Payload.TryGetValue("timeoutType", out var kind) && !string.Equals(kind, "Team", StringComparison.Ordinal))
        {
            return RuleResult.Valid;
        }

        var remaining = c.State.TimeoutsRemaining.TryGetValue(team, out var value) ? value : 0;
        return remaining > 0
            ? RuleResult.Valid
            : RuleResult.Overridable(Code, "That team has no timeouts remaining.");
    }
}

/// <summary>A period should not end with time still on the clock.</summary>
public sealed class PeriodCompleteRule : IEventRule
{
    /// <inheritdoc />
    public string Code => "PERIOD_NOT_COMPLETE";

    /// <inheritdoc />
    public RuleResult Evaluate(ValidationContext c, GameEvent e)
        => e.EventType == EventTypes.PeriodEnd && e.GameClockMs > 0
            ? RuleResult.Overridable(Code, "The period still has time remaining.")
            : RuleResult.Valid;
}
