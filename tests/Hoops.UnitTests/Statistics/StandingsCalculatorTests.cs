using FluentAssertions;
using Hoops.Modules.Statistics.Domain;
using Hoops.SharedKernel;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.UnitTests.Statistics;

/// <summary>
/// The tiebreaker order is a competition rule with real consequences — it decides who advances — so it
/// is tested directly against manufactured ties rather than only through the API.
/// </summary>
public sealed class StandingsCalculatorTests
{
    private static readonly CompetitionTeamId A = CompetitionTeamId.FromGuid(new Guid("00000000-0000-0000-0000-0000000000a1"));
    private static readonly CompetitionTeamId B = CompetitionTeamId.FromGuid(new Guid("00000000-0000-0000-0000-0000000000b2"));
    private static readonly CompetitionTeamId C = CompetitionTeamId.FromGuid(new Guid("00000000-0000-0000-0000-0000000000c3"));
    private static readonly CompetitionTeamId D = CompetitionTeamId.FromGuid(new Guid("00000000-0000-0000-0000-0000000000d4"));

    [Fact]
    public void League_points_order_the_table_before_any_tiebreaker()
    {
        var results = Pair(A, B, 80, 70).Concat(Pair(A, C, 90, 60)).Concat(Pair(B, C, 75, 70)).ToList();
        var rules = RuleSet.Fiba();

        var table = StandingsCalculator.Order(StandingsCalculator.Build(results, rules), results, rules);

        table.Select(t => t.TeamId).Should().Equal(A, B, C);
        table[0].Won.Should().Be(2);
        table[0].LeaguePoints.Should().Be(4, "two wins at 2 points each");
        table[2].LeaguePoints.Should().Be(2, "two losses at 1 point each");
    }

    [Fact]
    public void A_three_way_tie_resolves_through_the_configured_order()
    {
        // Each team wins one and loses one, so all three finish level on league points.
        // A beat B by 20, B beat C by 10, C beat A by 1.
        var results = Pair(A, B, 90, 70).Concat(Pair(B, C, 80, 70)).Concat(Pair(C, A, 71, 70)).ToList();
        var rules = RuleSet.Fiba();

        var table = StandingsCalculator.Order(StandingsCalculator.Build(results, rules), results, rules);

        table.Should().HaveCount(3);
        table.Select(t => t.LeaguePoints).Distinct().Should().ContainSingle("the tie is genuine");

        // Head-to-head wins are 1 apiece, so that criterion separates nothing and the next one applies:
        // point differential — A +19 (+20, -1), C -9 (-10, +1), B -10 (-20, +10).
        table.Select(t => t.TeamId).Should().Equal(A, C, B);
        table[0].PointDifferential.Should().Be(19);
        table[1].PointDifferential.Should().Be(-9);
        table[2].PointDifferential.Should().Be(-10);
    }

    [Fact]
    public void The_configured_order_changes_the_result()
    {
        // A and B each win once, so they tie on league points. A has the better differential (+20 vs
        // +15); B scored more (100 vs 60). They beat DIFFERENT opponents, so the losers tie with each
        // other rather than with A and B.
        var results = Pair(A, C, 60, 40).Concat(Pair(B, D, 100, 85)).ToList();

        var byDifferential = RuleSet.Fiba() with { Tiebreakers = [StandingsTiebreaker.PointDifferential] };
        var byPointsScored = RuleSet.Fiba() with { Tiebreakers = [StandingsTiebreaker.PointsScored] };

        var first = StandingsCalculator.Order(StandingsCalculator.Build(results, byDifferential), results, byDifferential);
        var second = StandingsCalculator.Order(StandingsCalculator.Build(results, byPointsScored), results, byPointsScored);

        first[0].TeamId.Should().Be(A, "A has the better differential");
        second[0].TeamId.Should().Be(B, "B scored more points");
    }

    [Fact]
    public void An_unbreakable_tie_orders_deterministically_rather_than_arbitrarily()
    {
        var results = Pair(A, C, 70, 60).Concat(Pair(B, C, 70, 60)).ToList();
        var rules = RuleSet.Fiba();

        var once = StandingsCalculator.Order(StandingsCalculator.Build(results, rules), results, rules);
        var twice = StandingsCalculator.Order(StandingsCalculator.Build(results, rules), results, rules);

        once.Select(t => t.TeamId).Should().Equal(twice.Select(t => t.TeamId),
            "a recompute must not shuffle the table");
    }

    [Fact]
    public void League_points_follow_the_rule_sets_win_and_loss_values()
    {
        var results = Pair(A, B, 80, 70).ToList();
        var rules = RuleSet.Fiba() with { PointsForWin = 3, PointsForLoss = 0 };

        var table = StandingsCalculator.Build(results, rules);

        table.Single(t => t.TeamId == A).LeaguePoints.Should().Be(3);
        table.Single(t => t.TeamId == B).LeaguePoints.Should().Be(0);
    }

    /// <summary>Both sides of one game, as the calculator consumes them.</summary>
    private static IEnumerable<StandingsResult> Pair(
        CompetitionTeamId home, CompetitionTeamId away, int homePoints, int awayPoints)
    {
        yield return new StandingsResult(home, away, homePoints, awayPoints);
        yield return new StandingsResult(away, home, awayPoints, homePoints);
    }
}
