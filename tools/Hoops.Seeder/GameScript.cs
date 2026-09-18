namespace Hoops.Seeder;

/// <summary>One event as the scorer's table would submit it.</summary>
public sealed record ScriptedEvent(
    string EventType,
    string? EventSubtype,
    int Period,
    int GameClockMs,
    string? CompetitionTeamId = null,
    string? GameRosterEntryId = null,
    string? SecondaryRosterEntryId = null,
    int? ShotXCm = null,
    int? ShotYCm = null,
    Dictionary<string, string>? Payload = null,
    bool Override = false,
    string? OverrideReason = null);

/// <summary>A team as the script sees it: its id and the frozen game-roster rows.</summary>
public sealed record ScriptTeam(string CompetitionTeamId, IReadOnlyList<string> Starters, IReadOnlyList<string> Bench);

/// <summary>
/// Writes a realistic four-period game as a sequence of events. Deterministic for a given seed, so
/// re-running the seeder reproduces the same games. This is where the seeder earns its keep: three
/// of these give every leaderboard, box score and shot chart something real to show.
/// </summary>
public static class GameScript
{
    // Canonical court frame (§8, CourtGeometry): x runs from half-court (0) to the baseline (1400),
    // the hoop is at (1242.5, 0), the arc is 675 cm from it, and the corners are beyond |y| = 660.
    private static readonly (int X, int Y)[] TwoPointSpots =
    [
        (1200, 0), (1180, 60), (1100, -100),    // restricted area and just outside it
        (1000, 120), (950, -200), (880, 0),     // paint
        (1050, 400), (950, -450), (780, 150),   // mid-range
    ];

    private static readonly (int X, int Y)[] ThreePointSpots =
    [
        (540, 0), (580, 250), (580, -250),      // above the break, straight on and wings
        (700, 500), (700, -500),                // wings, deeper
        (1200, 700), (1200, -700),              // corners
    ];

    public static IReadOnlyList<ScriptedEvent> Write(
        int seed, ScriptTeam home, ScriptTeam away, int periodMs, bool includeOverride)
    {
        var random = new Random(seed);
        var events = new List<ScriptedEvent>();
        var fouls = new Dictionary<string, int>();
        var onCourt = new Dictionary<string, List<string>>
        {
            [home.CompetitionTeamId] = [.. home.Starters],
            [away.CompetitionTeamId] = [.. away.Starters],
        };
        var bench = new Dictionary<string, List<string>>
        {
            [home.CompetitionTeamId] = [.. home.Bench],
            [away.CompetitionTeamId] = [.. away.Bench],
        };
        var overrideWritten = !includeOverride;

        for (var period = 1; period <= 4; period++)
        {
            var clock = periodMs;
            events.Add(new ScriptedEvent("PERIOD_START", "Regulation", period, clock));
            events.Add(new ScriptedEvent("CLOCK_START", null, period, clock));

            var offence = period % 2 == 1 ? home : away;
            var defence = offence == home ? away : home;
            var possessions = 0;

            while (clock > 15_000)
            {
                clock -= random.Next(8_000, 22_000);
                if (clock < 1_000)
                {
                    break;
                }

                var shooter = Pick(random, onCourt[offence.CompetitionTeamId]);
                var roll = random.NextDouble();

                if (roll < 0.10)
                {
                    // Turnover: a steal by the defence.
                    var stealer = Pick(random, onCourt[defence.CompetitionTeamId]);
                    events.Add(new ScriptedEvent("TURNOVER", "Steal", period, clock, offence.CompetitionTeamId, shooter, stealer));
                }
                else if (roll < 0.22)
                {
                    // Shooting foul, then two free throws. The clock stops for them.
                    var fouler = PickWithFoulRoom(random, onCourt[defence.CompetitionTeamId], fouls);
                    if (fouler is null)
                    {
                        continue;
                    }

                    fouls[fouler] = fouls.GetValueOrDefault(fouler) + 1;
                    events.Add(new ScriptedEvent("CLOCK_STOP", null, period, clock));
                    events.Add(new ScriptedEvent(
                        "FOUL", "Shooting", period, clock, defence.CompetitionTeamId, fouler, shooter,
                        Payload: new() { ["freeThrowsAwarded"] = "2" }));

                    for (var attempt = 1; attempt <= 2; attempt++)
                    {
                        var made = random.NextDouble() < 0.72;
                        events.Add(new ScriptedEvent(
                            made ? "FREE_THROW_MADE" : "FREE_THROW_MISSED", null, period, clock,
                            offence.CompetitionTeamId, shooter,
                            Payload: new() { ["attemptNumber"] = attempt.ToString(), ["totalAttempts"] = "2" }));

                        if (!made && attempt == 2)
                        {
                            var rebounder = Pick(random, onCourt[defence.CompetitionTeamId]);
                            events.Add(new ScriptedEvent("REBOUND", "Defensive", period, clock, defence.CompetitionTeamId, rebounder));
                        }
                    }

                    // A dead ball is when substitutions happen.
                    MaybeSubstitute(random, events, period, clock, offence, onCourt, bench);
                    events.Add(new ScriptedEvent("CLOCK_START", null, period, clock));
                }
                else
                {
                    var three = random.NextDouble() < 0.35;
                    var spot = three ? Pick(random, ThreePointSpots) : Pick(random, TwoPointSpots);
                    var made = random.NextDouble() < (three ? 0.36 : 0.50);
                    var subtype = three ? "ThreePoint" : "TwoPoint";

                    if (made)
                    {
                        // Roughly half of made baskets are assisted.
                        var assister = random.NextDouble() < 0.55
                            ? Pick(random, onCourt[offence.CompetitionTeamId].Where(p => p != shooter).ToList())
                            : null;
                        events.Add(new ScriptedEvent(
                            "FIELD_GOAL_MADE", subtype, period, clock, offence.CompetitionTeamId, shooter, assister, spot.X, spot.Y));

                        if (!overrideWritten && period == 2)
                        {
                            // The deliberate override: the scorer taps a rebound after a MADE basket.
                            // REBOUND_WITHOUT_MISS is tier 2, so it goes through with a reason and is
                            // flagged for the review screen.
                            overrideWritten = true;
                            var rebounder = Pick(random, onCourt[defence.CompetitionTeamId]);
                            events.Add(new ScriptedEvent(
                                "REBOUND", "Defensive", period, clock, defence.CompetitionTeamId, rebounder,
                                Override: true, OverrideReason: "Scorer's table: shot was actually missed, correcting live"));
                        }
                    }
                    else
                    {
                        // A block is the secondary actor on the miss, not an event of its own.
                        var blocker = random.NextDouble() < 0.08 ? Pick(random, onCourt[defence.CompetitionTeamId]) : null;
                        events.Add(new ScriptedEvent(
                            "FIELD_GOAL_MISSED", subtype, period, clock, offence.CompetitionTeamId, shooter, blocker, spot.X, spot.Y));

                        var offensiveBoard = random.NextDouble() < 0.28;
                        var side = offensiveBoard ? offence : defence;
                        var rebounder = Pick(random, onCourt[side.CompetitionTeamId]);
                        events.Add(new ScriptedEvent(
                            "REBOUND", offensiveBoard ? "Offensive" : "Defensive", period, clock, side.CompetitionTeamId, rebounder));

                        if (offensiveBoard)
                        {
                            continue; // same team keeps the ball
                        }
                    }
                }

                (offence, defence) = (defence, offence);
                possessions++;

                // One timeout per team per half, at a natural break.
                if (possessions == 12 && (period == 1 || period == 3))
                {
                    events.Add(new ScriptedEvent("CLOCK_STOP", null, period, clock));
                    events.Add(new ScriptedEvent("TIMEOUT", null, period, clock, offence.CompetitionTeamId));
                    MaybeSubstitute(random, events, period, clock, offence, onCourt, bench, force: true);
                    MaybeSubstitute(random, events, period, clock, defence, onCourt, bench, force: true);
                    events.Add(new ScriptedEvent("CLOCK_START", null, period, clock));
                }
            }

            events.Add(new ScriptedEvent("CLOCK_STOP", null, period, 0));
            events.Add(new ScriptedEvent("PERIOD_END", null, period, 0));
        }

        return events;
    }

    private static void MaybeSubstitute(
        Random random, List<ScriptedEvent> events, int period, int clock, ScriptTeam team,
        Dictionary<string, List<string>> onCourt, Dictionary<string, List<string>> bench, bool force = false)
    {
        if (!force && random.NextDouble() > 0.4)
        {
            return;
        }

        var court = onCourt[team.CompetitionTeamId];
        var sideline = bench[team.CompetitionTeamId];
        if (sideline.Count == 0)
        {
            return;
        }

        var outgoing = Pick(random, court);
        var incoming = Pick(random, sideline);
        events.Add(new ScriptedEvent("SUBSTITUTION", null, period, clock, team.CompetitionTeamId, outgoing, incoming));

        court.Remove(outgoing);
        court.Add(incoming);
        sideline.Remove(incoming);
        sideline.Add(outgoing);
    }

    private static string? PickWithFoulRoom(Random random, List<string> players, Dictionary<string, int> fouls)
    {
        // Stay one short of the limit: the seed exists to produce clean, complete games.
        var eligible = players.Where(p => fouls.GetValueOrDefault(p) < 4).ToList();
        return eligible.Count == 0 ? null : Pick(random, eligible);
    }

    private static T Pick<T>(Random random, IReadOnlyList<T> items) => items[random.Next(items.Count)];
}
