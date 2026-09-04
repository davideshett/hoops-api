namespace Hoops.Modules.GameRecording.Domain;

/// <summary>
/// Pure fixture-generation algorithms. Round-robin uses the circle method; knockout uses standard
/// bracket seeding. Deterministic and side-effect free, so they are exhaustively unit-testable.
/// </summary>
public static class FixtureGenerator
{
    /// <summary>
    /// Round-robin schedule (circle method). Single: every pair once — n(n-1)/2 games. Double: every
    /// pair twice with home and away swapped — n(n-1) games.
    /// </summary>
    public static IReadOnlyList<(T Home, T Away)> RoundRobin<T>(IReadOnlyList<T> teams, bool doubleRound)
    {
        var n = teams.Count;
        if (n < 2)
        {
            return [];
        }

        var arrangement = Enumerable.Range(0, n).ToList();
        if (n % 2 != 0)
        {
            arrangement.Add(-1); // odd count → a bye marker sits out each round
        }

        var m = arrangement.Count;
        var half = m / 2;
        var pairs = new List<(T, T)>();

        for (var round = 0; round < m - 1; round++)
        {
            for (var i = 0; i < half; i++)
            {
                var a = arrangement[i];
                var b = arrangement[m - 1 - i];
                if (a == -1 || b == -1)
                {
                    continue; // the bye
                }

                // Alternate home/away by round for a balanced single leg.
                var (home, away) = round % 2 == 0 ? (a, b) : (b, a);
                pairs.Add((teams[home], teams[away]));
            }

            // Rotate every position except the first, clockwise.
            var last = arrangement[m - 1];
            for (var i = m - 1; i > 1; i--)
            {
                arrangement[i] = arrangement[i - 1];
            }

            arrangement[1] = last;
        }

        if (doubleRound)
        {
            // The reverse leg swaps home and away for every fixture.
            pairs.AddRange(pairs.Select(p => (p.Item2, p.Item1)).ToList());
        }

        return pairs;
    }

    /// <summary>
    /// First-round knockout pairings from a seeded list (index 0 = top seed). Uses standard bracket
    /// seeding (1 v n, 2 v n-1, …); when the count is not a power of two, the top seeds receive byes
    /// and no game is emitted for those slots.
    /// </summary>
    public static IReadOnlyList<(T Home, T Away)> Knockout<T>(IReadOnlyList<T> seededTeams)
    {
        var n = seededTeams.Count;
        if (n < 2)
        {
            return [];
        }

        var bracket = 1;
        while (bracket < n)
        {
            bracket <<= 1;
        }

        var slots = SeedSlots(bracket);
        var pairs = new List<(T, T)>();
        for (var i = 0; i < slots.Count; i += 2)
        {
            var high = slots[i];      // 1-based seed
            var low = slots[i + 1];
            if (high <= n && low <= n)
            {
                pairs.Add((seededTeams[high - 1], seededTeams[low - 1]));
            }
            // Otherwise the present seed has a bye — no first-round game.
        }

        return pairs;
    }

    // The classic recursive bracket seed order, e.g. bracket 8 → [1,8,4,5,2,7,3,6].
    private static List<int> SeedSlots(int bracket)
    {
        var seeds = new List<int> { 1 };
        while (seeds.Count < bracket)
        {
            var rounds = seeds.Count * 2 + 1;
            var next = new List<int>(seeds.Count * 2);
            foreach (var seed in seeds)
            {
                next.Add(seed);
                next.Add(rounds - seed);
            }

            seeds = next;
        }

        return seeds;
    }
}
