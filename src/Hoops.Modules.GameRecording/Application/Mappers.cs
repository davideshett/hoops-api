using Hoops.Modules.GameRecording.Contracts;
using Hoops.Modules.GameRecording.Domain;
using Hoops.Modules.GameRecording.Domain.Projection;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.GameRecording.Application;

/// <summary>Hand-written domain → DTO mappings (no AutoMapper).</summary>
internal static class Mappers
{
    public static GameEventDto ToDto(this GameEvent e)
        => new(e.Id, e.Sequence, e.EventType, e.EventSubtype, e.Period, e.GameClockMs, e.CompetitionTeamId,
            e.GameRosterEntryId, e.SecondaryRosterEntryId, e.Points, e.ShotXCm, e.ShotYCm,
            e.ShotZone?.ToString(), e.ShotDistanceCm, e.IsVoided, e.WasOverridden,
            new Dictionary<string, string>(e.Payload));

    public static LiveGameStateDto ToDto(this LiveGameState s)
        => new(s.GameId, s.LastSequence, s.CurrentPeriod, s.GameClockMs, s.ClockRunning, s.GameStarted,
            s.GameEnded, s.PeriodEnded,
            Keyed(s.Score), Keyed(s.TeamFouls), Keyed(s.TimeoutsRemaining),
            s.OnCourt.ToDictionary(kv => kv.Key.Value.ToString(), kv => kv.Value),
            s.FouledOut);

    public static PlayerStatlineDto ToDto(this PlayerStatline p)
        => new(p.GameRosterEntryId, p.PlayerId, p.CompetitionTeamId, p.Points, p.FieldGoalsMade,
            p.FieldGoalsAttempted, p.ThreePointersMade, p.ThreePointersAttempted, p.FreeThrowsMade,
            p.FreeThrowsAttempted, p.OffensiveRebounds, p.DefensiveRebounds, p.TotalRebounds, p.Assists,
            p.Steals, p.Blocks, p.Turnovers, p.FoulsCommitted, p.FoulsDrawn, p.FouledOut, p.SecondsPlayed,
            p.PlusMinus, p.FieldGoalPercentage, p.ThreePointPercentage, p.FreeThrowPercentage, p.Efficiency);

    public static TeamStatlineDto ToDto(this TeamStatline t)
        => new(t.CompetitionTeamId, t.Points, t.FieldGoalsMade, t.FieldGoalsAttempted, t.ThreePointersMade,
            t.ThreePointersAttempted, t.FreeThrowsMade, t.FreeThrowsAttempted, t.OffensiveRebounds,
            t.DefensiveRebounds, t.TotalRebounds, t.Assists, t.Steals, t.Blocks, t.Turnovers, t.FoulsCommitted);

    // JSON object keys must be strings; ids serialise as their Guid text.
    private static IReadOnlyDictionary<string, int> Keyed(IReadOnlyDictionary<CompetitionTeamId, int> source)
        => source.ToDictionary(kv => kv.Key.Value.ToString(), kv => kv.Value);

    public static GameDto ToDto(this Game g)
        => new(g.Id, g.CompetitionId, g.StageId, g.GroupId, g.HomeCompetitionTeamId, g.AwayCompetitionTeamId,
            g.VenueId, g.ScheduledAt, g.Status.ToString(), g.RuleSetSnapshot, g.RosterLockedAt);

    public static GameOfficialDto ToDto(this GameOfficial o)
        => new(o.Id, o.GameId, o.FullName, o.Role.ToString());

    public static GameRosterEntryDto ToDto(this GameRosterEntry e, string fullName)
        => new(e.Id, e.CompetitionTeamId, e.PlayerId, fullName, e.JerseyNumber, e.Position, e.IsStarter, e.IsCaptain);
}
