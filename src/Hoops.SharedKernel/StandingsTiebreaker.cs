namespace Hoops.SharedKernel;

/// <summary>
/// A criterion for breaking a tie in the standings table. Applied in the order the competition's rule
/// set lists them, until the tie resolves.
/// </summary>
public enum StandingsTiebreaker
{
    /// <summary>Record in games played between only the tied teams.</summary>
    HeadToHead = 0,

    /// <summary>Points scored minus points conceded, across the competition.</summary>
    PointDifferential = 1,

    /// <summary>Total points scored.</summary>
    PointsScored = 2,

    /// <summary>Total wins.</summary>
    Wins = 3,

    /// <summary>Point differential in games between only the tied teams.</summary>
    HeadToHeadDifferential = 4,
}
