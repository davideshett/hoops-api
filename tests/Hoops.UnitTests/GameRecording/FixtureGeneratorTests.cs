using FluentAssertions;
using Hoops.Modules.GameRecording.Domain;

namespace Hoops.UnitTests.GameRecording;

public sealed class FixtureGeneratorTests
{
    private static readonly IReadOnlyList<int> Eight = Enumerable.Range(1, 8).ToList();

    [Fact]
    public void Single_round_robin_for_eight_teams_produces_28_fixtures_each_pairing_once()
    {
        var fixtures = FixtureGenerator.RoundRobin(Eight, doubleRound: false);

        fixtures.Should().HaveCount(28);
        fixtures.Should().OnlyContain(f => f.Home != f.Away, "no team plays itself");

        var pairings = fixtures.Select(f => Unordered(f.Home, f.Away)).ToList();
        pairings.Should().OnlyHaveUniqueItems("each pair meets exactly once");
        pairings.Should().HaveCount(28);
    }

    [Fact]
    public void Double_round_robin_produces_56_with_home_and_away_swapped()
    {
        var fixtures = FixtureGenerator.RoundRobin(Eight, doubleRound: true);

        fixtures.Should().HaveCount(56);

        // Every unordered pair appears exactly twice, once each way.
        foreach (var group in fixtures.GroupBy(f => Unordered(f.Home, f.Away)))
        {
            group.Should().HaveCount(2);
            group.Select(f => (f.Home, f.Away)).Distinct().Should().HaveCount(2, "home and away are swapped between legs");
        }
    }

    [Fact]
    public void Round_robin_handles_an_odd_number_of_teams_with_a_bye()
    {
        var five = Enumerable.Range(1, 5).ToList();

        var fixtures = FixtureGenerator.RoundRobin(five, doubleRound: false);

        fixtures.Should().HaveCount(10); // 5*4/2
        fixtures.Select(f => Unordered(f.Home, f.Away)).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Knockout_seeds_eight_teams_into_four_first_round_games()
    {
        var fixtures = FixtureGenerator.Knockout(Eight);

        fixtures.Should().HaveCount(4);
        // Standard seeding: 1v8, 2v7, 3v6, 4v5.
        fixtures.Select(f => Unordered(f.Home, f.Away)).Should().BeEquivalentTo(new[]
        {
            Unordered(1, 8), Unordered(2, 7), Unordered(3, 6), Unordered(4, 5),
        });
        fixtures.Should().Contain((1, 8), "the top seed hosts the bottom seed");
    }

    [Fact]
    public void Knockout_with_a_non_power_of_two_gives_top_seeds_byes()
    {
        var six = Enumerable.Range(1, 6).ToList();

        var fixtures = FixtureGenerator.Knockout(six);

        // Bracket of 8: seeds 1 and 2 get byes (opponents 8 and 7 don't exist) → 2 first-round games.
        fixtures.Should().HaveCount(2);
        fixtures.Select(f => Unordered(f.Home, f.Away)).Should().BeEquivalentTo(new[]
        {
            Unordered(4, 5), Unordered(3, 6),
        });
    }

    private static (int, int) Unordered(int a, int b) => a < b ? (a, b) : (b, a);
}
