using System.Text.Json;
using FluentAssertions;
using Hoops.Modules.Competitions.Domain;
using Hoops.SharedKernel;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.UnitTests.Competitions;

public sealed class DomainTests
{
    private static readonly OrganisationId Org = OrganisationId.New();

    [Fact]
    public void RuleSet_round_trips_through_json_without_loss()
    {
        var original = new RuleSet
        {
            NumberOfPeriods = 2,
            PeriodDurationSeconds = 480,
            PlayersOnCourt = 3,
            PersonalFoulLimit = 6,
            AllowsTies = true,
            MinimumIdentityTier = 2,
            ShotClockSeconds = 14,
        };

        var json = JsonSerializer.Serialize(original, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<RuleSet>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        restored.Should().Be(original);
    }

    [Fact]
    public void RuleSet_defaults_are_fiba()
    {
        var fiba = RuleSet.Fiba();

        fiba.NumberOfPeriods.Should().Be(4);
        fiba.PeriodDurationSeconds.Should().Be(600);
        fiba.PlayersOnCourt.Should().Be(5);
        fiba.PersonalFoulLimit.Should().Be(5);
        fiba.ShotClockSeconds.Should().Be(24);
        fiba.AllowsTies.Should().BeFalse();
    }

    [Theory]
    [InlineData(CompetitionStatus.Registration, true)]
    [InlineData(CompetitionStatus.Archived, true)]
    [InlineData(CompetitionStatus.InProgress, false)]
    [InlineData(CompetitionStatus.Completed, false)]
    public void Competition_transitions_from_draft_are_gated(CompetitionStatus target, bool allowed)
    {
        var competition = NewCompetition();

        competition.TransitionTo(target).Should().Be(allowed);
        if (allowed)
        {
            competition.Status.Should().Be(target);
        }
        else
        {
            competition.Status.Should().Be(CompetitionStatus.Draft);
        }
    }

    [Fact]
    public void Competition_full_lifecycle_is_allowed_in_order()
    {
        var competition = NewCompetition();

        competition.TransitionTo(CompetitionStatus.Registration).Should().BeTrue();
        competition.TransitionTo(CompetitionStatus.InProgress).Should().BeTrue();
        competition.TransitionTo(CompetitionStatus.Completed).Should().BeTrue();
        competition.TransitionTo(CompetitionStatus.Archived).Should().BeTrue();
    }

    [Fact]
    public void Competition_create_lowercases_slug_and_defaults_rule_set()
    {
        var competition = Competition.Create(
            Org, SeasonId.New(), "Premier League", "Premier-LEAGUE",
            CompetitionFormat.League, category: null, ruleSet: null, timezone: "Africa/Lagos");

        competition.Slug.Should().Be("premier-league");
        competition.RuleSet.Should().Be(RuleSet.Fiba());
        competition.Status.Should().Be(CompetitionStatus.Draft);
    }

    [Fact]
    public void Season_cannot_end_before_it_starts()
    {
        var act = () => Season.Create(Org, "2025/26", new DateOnly(2026, 6, 30), new DateOnly(2025, 10, 1));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Team_short_name_is_length_limited()
    {
        var act = () => Team.Create(Org, "A Very Long Club Name", "ThisIsWayTooLong", null, null, null, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CompetitionTeam_enters_as_registered()
    {
        var entry = CompetitionTeam.Enter(Org, CompetitionId.New(), TeamId.New());

        entry.Status.Should().Be(CompetitionTeamStatus.Registered);
        entry.OrganisationId.Should().Be(Org);
    }

    private static Competition NewCompetition()
        => Competition.Create(Org, SeasonId.New(), "Cup", "cup", CompetitionFormat.Knockout, null, null, "UTC");
}
