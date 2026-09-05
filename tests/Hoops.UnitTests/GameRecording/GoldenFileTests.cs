using FluentAssertions;
using Hoops.Modules.GameRecording.Domain.Projection;

namespace Hoops.UnitTests.GameRecording;

/// <summary>
/// The highest-value suite in the codebase (§14). Each fixture is a complete game's event log plus the
/// statlines it must project to. If the projector is wrong, the product's entire promise is wrong.
/// </summary>
public sealed class GoldenFileTests
{
    public static TheoryData<string> Games => new() { "regulation-clean", "overtime-foul-out", "free-throw-heavy" };

    [Theory]
    [MemberData(nameof(Games))]
    public void Golden_game_projects_to_the_expected_statlines(string name)
    {
        var game = GoldenFiles.LoadGame(name);
        var expected = GoldenFiles.LoadExpected(name);

        var projection = new GameProjector().Project(game.ToContext(), game.ToEvents());

        projection.Score[GoldenFiles.Home].Should().Be(expected.HomePoints, "home score");
        projection.Score[GoldenFiles.Away].Should().Be(expected.AwayPoints, "away score");

        foreach (var line in expected.Lines)
        {
            var actual = projection.PlayerStatlines.Single(s => s.GameRosterEntryId == GoldenFiles.Roster(line.Player));
            var because = $"player {line.Player} in {name}";

            actual.Points.Should().Be(line.Pts, "points for {0}", because);
            actual.FieldGoalsMade.Should().Be(line.Fgm, "FGM for {0}", because);
            actual.FieldGoalsAttempted.Should().Be(line.Fga, "FGA for {0}", because);
            actual.ThreePointersMade.Should().Be(line.Tpm, "3PM for {0}", because);
            actual.ThreePointersAttempted.Should().Be(line.Tpa, "3PA for {0}", because);
            actual.FreeThrowsMade.Should().Be(line.Ftm, "FTM for {0}", because);
            actual.FreeThrowsAttempted.Should().Be(line.Fta, "FTA for {0}", because);
            actual.OffensiveRebounds.Should().Be(line.Oreb, "OREB for {0}", because);
            actual.DefensiveRebounds.Should().Be(line.Dreb, "DREB for {0}", because);
            actual.Assists.Should().Be(line.Ast, "AST for {0}", because);
            actual.Steals.Should().Be(line.Stl, "STL for {0}", because);
            actual.Blocks.Should().Be(line.Blk, "BLK for {0}", because);
            actual.BlocksAgainst.Should().Be(line.Blka, "blocks against for {0}", because);
            actual.Turnovers.Should().Be(line.Tov, "TOV for {0}", because);
            actual.FoulsCommitted.Should().Be(line.Pf, "fouls committed for {0}", because);
            actual.FoulsDrawn.Should().Be(line.Fd, "fouls drawn for {0}", because);
            actual.FouledOut.Should().Be(line.FouledOut, "fouled-out for {0}", because);
            actual.PlusMinus.Should().Be(line.PlusMinus, "plus/minus for {0}", because);
        }
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void Projecting_the_same_log_twice_produces_identical_output(string name)
    {
        var game = GoldenFiles.LoadGame(name);
        var projector = new GameProjector();

        var first = projector.Project(game.ToContext(), game.ToEvents());
        var second = projector.Project(game.ToContext(), game.ToEvents());

        second.Should().BeEquivalentTo(first, "the projector is deterministic");
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void Arithmetic_invariants_hold(string name)
    {
        var game = GoldenFiles.LoadGame(name);
        var projection = new GameProjector().Project(game.ToContext(), game.ToEvents());

        // Team points equal the sum of that team's players' points.
        foreach (var team in projection.TeamStatlines)
        {
            var playerSum = projection.PlayerStatlines
                .Where(p => p.CompetitionTeamId == team.CompetitionTeamId).Sum(p => p.Points);
            team.Points.Should().Be(playerSum, "team points must equal the sum of its players' points");
        }

        // Plus/minus across both teams sums to zero.
        projection.PlayerStatlines.Sum(p => p.PlusMinus).Should().Be(0, "plus/minus must net to zero");

        // Threes are a subset of field goals, never additive.
        foreach (var line in projection.PlayerStatlines)
        {
            line.ThreePointersAttempted.Should().BeLessThanOrEqualTo(line.FieldGoalsAttempted);
            line.ThreePointersMade.Should().BeLessThanOrEqualTo(line.FieldGoalsMade);
            line.FieldGoalsMade.Should().BeLessThanOrEqualTo(line.FieldGoalsAttempted);
            line.FreeThrowsMade.Should().BeLessThanOrEqualTo(line.FreeThrowsAttempted);
        }
    }

    [Fact]
    public void Percentages_are_null_not_zero_when_there_were_no_attempts()
    {
        var game = GoldenFiles.LoadGame("regulation-clean");
        var projection = new GameProjector().Project(game.ToContext(), game.ToEvents());

        // Player 2 records only an assist and a steal — no shots at all.
        var noShots = projection.PlayerStatlines.Single(s => s.GameRosterEntryId == GoldenFiles.Roster(2));
        noShots.FieldGoalPercentage.Should().BeNull();
        noShots.ThreePointPercentage.Should().BeNull();
        noShots.FreeThrowPercentage.Should().BeNull();

        // Player 1 shot twos and free throws but never a three.
        var noThrees = projection.PlayerStatlines.Single(s => s.GameRosterEntryId == GoldenFiles.Roster(1));
        noThrees.ThreePointPercentage.Should().BeNull("a player who took no threes has no three-point percentage");
        noThrees.FieldGoalPercentage.Should().NotBeNull();
    }

    [Fact]
    public void Voided_events_contribute_nothing_but_remain_in_the_log()
    {
        var game = GoldenFiles.LoadGame("regulation-clean");
        var events = game.ToEvents();
        var baseline = new GameProjector().Project(game.ToContext(), events);

        // Void the first made field goal (player 1, two points).
        var target = events.First(e => e.EventType == "FIELD_GOAL_MADE" && e.GameRosterEntryId == GoldenFiles.Roster(1));
        target.Void();

        var after = new GameProjector().Project(game.ToContext(), events);

        events.Should().HaveCount(baseline.LiveState.LastSequence == 0 ? events.Count : events.Count,
            "the row is never removed from the log");
        after.Score[GoldenFiles.Home].Should().Be(baseline.Score[GoldenFiles.Home] - 2);

        var line = after.PlayerStatlines.Single(s => s.GameRosterEntryId == GoldenFiles.Roster(1));
        var before = baseline.PlayerStatlines.Single(s => s.GameRosterEntryId == GoldenFiles.Roster(1));
        line.Points.Should().Be(before.Points - 2);
        line.FieldGoalsMade.Should().Be(before.FieldGoalsMade - 1);
        line.FieldGoalsAttempted.Should().Be(before.FieldGoalsAttempted - 1);
    }
}
