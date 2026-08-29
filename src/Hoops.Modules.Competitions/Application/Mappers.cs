using Hoops.Modules.Competitions.Contracts;
using Hoops.Modules.Competitions.Domain;

namespace Hoops.Modules.Competitions.Application;

/// <summary>Hand-written domain → DTO mappings (no AutoMapper).</summary>
internal static class Mappers
{
    public static SeasonDto ToDto(this Season s)
        => new(s.Id, s.Name, s.StartsOn, s.EndsOn, s.IsActive, s.CreatedAt);

    public static CompetitionDto ToDto(this Competition c)
        => new(c.Id, c.SeasonId, c.Name, c.Slug, c.Format.ToString(), c.Category, c.RuleSet,
            c.Status.ToString(), c.StartsOn, c.EndsOn, c.Timezone, c.CreatedAt);

    public static StageDto ToDto(this Stage s)
        => new(s.Id, s.CompetitionId, s.Name, s.StageType.ToString(), s.Sequence);

    public static GroupDto ToDto(this Group g)
        => new(g.Id, g.StageId, g.Name);

    public static TeamDto ToDto(this Team t)
        => new(t.Id, t.Name, t.ShortName, t.Abbreviation, t.LogoUrl, t.PrimaryColour, t.SecondaryColour,
            t.HomeVenueId, t.CreatedAt);

    public static CompetitionTeamDto ToDto(this CompetitionTeam ct)
        => new(ct.Id, ct.CompetitionId, ct.TeamId, ct.GroupId, ct.DisplayName, ct.Seed, ct.Status.ToString());

    public static TeamStaffDto ToDto(this TeamStaff s)
        => new(s.Id, s.CompetitionTeamId, s.FullName, s.Role.ToString());

    public static VenueDto ToDto(this Venue v)
        => new(v.Id, v.Name, v.Address, v.City, v.CourtCount, v.Timezone, v.CreatedAt);
}
