using Hoops.Modules.GameRecording.Contracts;
using Hoops.Modules.GameRecording.Domain;

namespace Hoops.Modules.GameRecording.Application;

/// <summary>Hand-written domain → DTO mappings (no AutoMapper).</summary>
internal static class Mappers
{
    public static GameDto ToDto(this Game g)
        => new(g.Id, g.CompetitionId, g.StageId, g.GroupId, g.HomeCompetitionTeamId, g.AwayCompetitionTeamId,
            g.VenueId, g.ScheduledAt, g.Status.ToString(), g.RuleSetSnapshot, g.RosterLockedAt);

    public static GameOfficialDto ToDto(this GameOfficial o)
        => new(o.Id, o.GameId, o.FullName, o.Role.ToString());

    public static GameRosterEntryDto ToDto(this GameRosterEntry e)
        => new(e.Id, e.CompetitionTeamId, e.PlayerId, e.JerseyNumber, e.Position, e.IsStarter, e.IsCaptain);
}
