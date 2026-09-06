using FluentAssertions;
using Hoops.Modules.GameRecording.Domain;
using Hoops.Modules.GameRecording.Domain.Projection;
using Hoops.SharedKernel;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.UnitTests.Statistics;

/// <summary>
/// The arithmetic that must hold for any correctly projected game. The projector asserts these itself
/// at the moment of creation; these tests prove the assertions are real and that the minutes
/// reconcile, which no in-projector check can express cheaply.
/// </summary>
public sealed class ProjectionInvariantTests
{
    private static readonly CompetitionTeamId Home = CompetitionTeamId.New();
    private static readonly CompetitionTeamId Away = CompetitionTeamId.New();
    private static readonly OrganisationId Org = OrganisationId.New();
    private static readonly GameId Game = GameId.New();
    private static readonly UserId Scorer = UserId.New();

    [Fact]
    public void A_full_period_reconciles_minutes_to_players_on_court_times_period_length()
    {
        var rules = RuleSet.Fiba() with { NumberOfPeriods = 1, PeriodDurationSeconds = 600 };
        var (context, players) = BuildContext(rules);
        var seq = 0L;

        var events = new List<GameEvent>
        {
            Ev(ref seq, EventTypes.GameStart, null, 1, 600_000),
            Ev(ref seq, EventTypes.PeriodStart, EventSubtypes.Regulation, 1, 600_000),
            Ev(ref seq, EventTypes.ClockStart, null, 1, 600_000),
            // A basket midway, so plus/minus has something to net out.
            Ev(ref seq, EventTypes.FieldGoalMade, EventSubtypes.TwoPoint, 1, 300_000,
                team: Home, actor: players[0].GameRosterEntryId, points: 2),
            Ev(ref seq, EventTypes.ClockStop, null, 1, 0),
            Ev(ref seq, EventTypes.PeriodEnd, null, 1, 0),
        };

        var projection = new GameProjector().Project(context, events);

        // Every player was on court for the whole running period.
        foreach (var team in new[] { Home, Away })
        {
            var teamSeconds = projection.PlayerStatlines
                .Where(p => p.CompetitionTeamId == team).Sum(p => p.SecondsPlayed);
            teamSeconds.Should().Be(rules.PlayersOnCourt * rules.PeriodDurationSeconds,
                "five players on court for a 600-second period is 3000 player-seconds");
        }
    }

    [Fact]
    public void Team_points_equal_the_sum_of_player_points_and_plus_minus_nets_to_zero()
    {
        var (context, players) = BuildContext(RuleSet.Fiba());
        var seq = 0L;

        var events = new List<GameEvent>
        {
            Ev(ref seq, EventTypes.GameStart, null, 1, 600_000),
            Ev(ref seq, EventTypes.PeriodStart, EventSubtypes.Regulation, 1, 600_000),
            Ev(ref seq, EventTypes.FieldGoalMade, EventSubtypes.ThreePoint, 1, 500_000,
                team: Home, actor: players[0].GameRosterEntryId, points: 3),
            Ev(ref seq, EventTypes.FieldGoalMade, EventSubtypes.TwoPoint, 1, 400_000,
                team: Away, actor: players[5].GameRosterEntryId, points: 2),
        };

        var projection = new GameProjector().Project(context, events);

        foreach (var team in projection.TeamStatlines)
        {
            projection.PlayerStatlines.Where(p => p.CompetitionTeamId == team.CompetitionTeamId)
                .Sum(p => p.Points).Should().Be(team.Points);
        }

        projection.PlayerStatlines.Sum(p => p.PlusMinus).Should().Be(0);
        projection.Score[Home].Should().Be(3);
        projection.Score[Away].Should().Be(2);
    }

    [Fact]
    public void A_substitution_splits_the_period_into_two_stints()
    {
        var rules = RuleSet.Fiba() with { PeriodDurationSeconds = 600 };
        var (context, players) = BuildContext(rules);
        var seq = 0L;

        var events = new List<GameEvent>
        {
            Ev(ref seq, EventTypes.GameStart, null, 1, 600_000),
            Ev(ref seq, EventTypes.PeriodStart, EventSubtypes.Regulation, 1, 600_000),
            Ev(ref seq, EventTypes.ClockStart, null, 1, 600_000),
            Ev(ref seq, EventTypes.ClockStop, null, 1, 400_000),
            // Home swaps its bench player in after 200 seconds.
            Ev(ref seq, EventTypes.Substitution, null, 1, 400_000,
                team: Home, actor: players[0].GameRosterEntryId, secondary: players[10].GameRosterEntryId),
            Ev(ref seq, EventTypes.ClockStart, null, 1, 400_000),
            Ev(ref seq, EventTypes.ClockStop, null, 1, 0),
            Ev(ref seq, EventTypes.PeriodEnd, null, 1, 0),
        };

        var projection = new GameProjector().Project(context, events);

        var homeStints = projection.LineupStints.Where(s => s.CompetitionTeamId == Home).ToList();
        homeStints.Should().HaveCount(2, "one lineup before the substitution and one after");
        homeStints.Sum(s => s.SecondsPlayed).Should().Be(600, "the two stints cover the whole period");
        homeStints[0].SecondsPlayed.Should().Be(200);
        homeStints[1].SecondsPlayed.Should().Be(400);

        // The substituted player's minutes match his stint; the replacement's match his.
        Statline(projection, players[0]).SecondsPlayed.Should().Be(200);
        Statline(projection, players[10]).SecondsPlayed.Should().Be(400);
    }

    private static PlayerStatline Statline(GameProjection projection, GamePlayer player)
        => projection.PlayerStatlines.Single(p => p.GameRosterEntryId == player.GameRosterEntryId);

    /// <summary>Five starters per team plus one home bench player.</summary>
    private static (GameContext Context, List<GamePlayer> Players) BuildContext(RuleSet rules)
    {
        var players = new List<GamePlayer>();
        for (var t = 0; t < 2; t++)
        {
            for (var p = 0; p < 5; p++)
            {
                players.Add(new GamePlayer(GameRosterEntryId.New(), t == 0 ? Home : Away, PlayerId.New(), true));
            }
        }

        players.Add(new GamePlayer(GameRosterEntryId.New(), Home, PlayerId.New(), false)); // index 10
        return (new GameContext(Game, Home, Away, players, rules), players);
    }

    private static GameEvent Ev(
        ref long seq, string type, string? subtype, int period, int clockMs,
        CompetitionTeamId? team = null, GameRosterEntryId? actor = null,
        GameRosterEntryId? secondary = null, int? points = null)
        => GameEvent.Record(
            Guid.CreateVersion7(), Org, Game, ++seq, type, subtype, period, clockMs,
            DateTimeOffset.UnixEpoch, Scorer,
            competitionTeamId: team, gameRosterEntryId: actor, secondaryRosterEntryId: secondary, points: points);
}
