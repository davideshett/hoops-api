using Hoops.SharedKernel;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Statistics.Domain;

/// <summary>One completed result, as the standings calculator needs it.</summary>
/// <param name="TeamId">The team.</param>
/// <param name="OpponentId">Their opponent.</param>
/// <param name="PointsFor">Points scored.</param>
/// <param name="PointsAgainst">Points conceded.</param>
public sealed record StandingsResult(
    CompetitionTeamId TeamId, CompetitionTeamId OpponentId, int PointsFor, int PointsAgainst);

/// <summary>A team's computed standings line, before a position is assigned.</summary>
/// <param name="TeamId">The team.</param>
/// <param name="Played">Games played.</param>
/// <param name="Won">Wins.</param>
/// <param name="Lost">Losses.</param>
/// <param name="Drawn">Draws.</param>
/// <param name="PointsFor">Points scored.</param>
/// <param name="PointsAgainst">Points conceded.</param>
/// <param name="LeaguePoints">League points from the rule set's win/loss/draw values.</param>
public sealed record StandingsLine(
    CompetitionTeamId TeamId, int Played, int Won, int Lost, int Drawn,
    int PointsFor, int PointsAgainst, int LeaguePoints)
{
    /// <summary>Point differential.</summary>
    public int PointDifferential => PointsFor - PointsAgainst;
}

/// <summary>
/// Builds and orders a standings table. PURE — same results in, same table out — so the tiebreaker
/// order can be tested directly against a manufactured tie.
/// </summary>
public static class StandingsCalculator
{
    /// <summary>Totals each team's results into an unordered set of standings lines.</summary>
    public static IReadOnlyList<StandingsLine> Build(IReadOnlyList<StandingsResult> results, RuleSet rules)
        => results.GroupBy(r => r.TeamId)
            .Select(g =>
            {
                var won = g.Count(r => r.PointsFor > r.PointsAgainst);
                var lost = g.Count(r => r.PointsFor < r.PointsAgainst);
                var drawn = g.Count(r => r.PointsFor == r.PointsAgainst);
                return new StandingsLine(
                    g.Key,
                    Played: g.Count(),
                    Won: won,
                    Lost: lost,
                    Drawn: drawn,
                    PointsFor: g.Sum(r => r.PointsFor),
                    PointsAgainst: g.Sum(r => r.PointsAgainst),
                    LeaguePoints: (won * rules.PointsForWin) + (lost * rules.PointsForLoss) + (drawn * rules.PointsForDraw));
            })
            .ToList();

    /// <summary>
    /// Orders the table: league points first, then the rule set's tiebreakers in order, applied only
    /// among the teams still tied. Head-to-head criteria are computed over the games between exactly
    /// those teams, which is why they cannot be a simple whole-table sort key.
    /// </summary>
    public static IReadOnlyList<StandingsLine> Order(
        IReadOnlyList<StandingsLine> lines, IReadOnlyList<StandingsResult> results, RuleSet rules)
    {
        var ordered = new List<StandingsLine>();

        foreach (var tiedGroup in lines.GroupBy(l => l.LeaguePoints).OrderByDescending(g => g.Key))
        {
            var group = tiedGroup.ToList();
            ordered.AddRange(group.Count == 1 ? group : BreakTie(group, results, rules));
        }

        return ordered;
    }

    private static List<StandingsLine> BreakTie(
        List<StandingsLine> tied, IReadOnlyList<StandingsResult> results, RuleSet rules)
    {
        var remaining = tied;

        foreach (var tiebreaker in rules.Tiebreakers)
        {
            var keyed = remaining.Select(line => (Line: line, Key: Key(tiebreaker, line, remaining, results))).ToList();

            // Sort by this criterion, then recurse into any sub-groups that are still level.
            var groups = keyed.GroupBy(x => x.Key).OrderByDescending(g => g.Key).ToList();
            if (groups.Count == 1)
            {
                continue; // this criterion separated nothing; try the next one
            }

            var result = new List<StandingsLine>();
            foreach (var group in groups)
            {
                var members = group.Select(x => x.Line).ToList();
                var next = rules.Tiebreakers.SkipWhile(t => t != tiebreaker).Skip(1).ToList();
                result.AddRange(members.Count == 1
                    ? members
                    : BreakTie(members, results, rules with { Tiebreakers = next }));
            }

            return result;
        }

        // Every configured tiebreaker was exhausted; order deterministically by id so the table is
        // stable across recomputes rather than arbitrary.
        return remaining.OrderBy(l => l.TeamId.Value).ToList();
    }

    private static int Key(
        StandingsTiebreaker tiebreaker, StandingsLine line,
        IReadOnlyList<StandingsLine> tied, IReadOnlyList<StandingsResult> results)
    {
        switch (tiebreaker)
        {
            case StandingsTiebreaker.PointDifferential:
                return line.PointDifferential;
            case StandingsTiebreaker.PointsScored:
                return line.PointsFor;
            case StandingsTiebreaker.Wins:
                return line.Won;

            case StandingsTiebreaker.HeadToHead:
            case StandingsTiebreaker.HeadToHeadDifferential:
            {
                // Only games between the tied teams count.
                var opponents = tied.Select(t => t.TeamId).ToHashSet();
                var mini = results.Where(r => r.TeamId == line.TeamId && opponents.Contains(r.OpponentId)).ToList();
                return tiebreaker == StandingsTiebreaker.HeadToHead
                    ? mini.Count(r => r.PointsFor > r.PointsAgainst)
                    : mini.Sum(r => r.PointsFor - r.PointsAgainst);
            }

            default:
                return 0;
        }
    }
}
