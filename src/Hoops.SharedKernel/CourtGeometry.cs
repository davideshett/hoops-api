namespace Hoops.SharedKernel;

/// <summary>Where a shot was taken from, in the canonical court frame (§8).</summary>
public enum ShotZone
{
    /// <summary>Within 125 cm of the hoop centre.</summary>
    RestrictedArea = 0,

    /// <summary>Inside the key, outside the restricted area.</summary>
    Paint = 1,

    /// <summary>Outside the paint, inside the three-point line.</summary>
    MidRange = 2,

    /// <summary>Beyond the corner three line, past the break point.</summary>
    CornerThree = 3,

    /// <summary>Beyond the arc, not a corner three.</summary>
    AboveBreakThree = 4,

    /// <summary>Behind half-way (negative x).</summary>
    Backcourt = 5,
}

/// <summary>
/// Pure court geometry in the canonical frame (§8): centimetres, origin at centre court, the attacking
/// basket always at positive x. The server recomputes zone and distance itself and never trusts
/// client-derived values.
/// </summary>
public static class CourtGeometry
{
    /// <summary>Half the court length, in cm (28 m court).</summary>
    public const int HalfLengthCm = 1400;

    /// <summary>Half the court width, in cm (15 m court).</summary>
    public const int HalfWidthCm = 750;

    /// <summary>Attacking hoop centre x, 1.575 m from the baseline.</summary>
    public const double HoopX = 1242.5;

    /// <summary>Attacking hoop centre y.</summary>
    public const double HoopY = 0;

    /// <summary>Three-point arc radius from the hoop centre, in cm.</summary>
    public const double ThreePointRadiusCm = 675;

    /// <summary>Corner three line: |y| greater than this is in the corner.</summary>
    public const double CornerThreeY = 660;

    /// <summary>Restricted-area radius from the hoop centre, in cm.</summary>
    public const double RestrictedAreaRadiusCm = 125;

    /// <summary>Half the paint width: the key spans |y| ≤ 245.</summary>
    public const double PaintHalfWidthCm = 245;

    /// <summary>The free-throw line / back edge of the paint.</summary>
    public const double FreeThrowLineX = 820;

    /// <summary>
    /// The x beyond which a shot outside the corner three line is a corner three:
    /// 1242.5 − √(675² − 660²) ≈ 1099.
    /// </summary>
    public static readonly double CornerBreakX =
        HoopX - Math.Sqrt((ThreePointRadiusCm * ThreePointRadiusCm) - (CornerThreeY * CornerThreeY));

    /// <summary>True if the point lies within the court bounds.</summary>
    public static bool IsInBounds(int x, int y)
        => x >= -HalfLengthCm && x <= HalfLengthCm && y >= -HalfWidthCm && y <= HalfWidthCm;

    /// <summary>Straight-line distance from the attacking hoop centre, in cm.</summary>
    public static double DistanceFromHoop(int x, int y)
    {
        var dx = x - HoopX;
        var dy = y - HoopY;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>Classifies a shot location into a <see cref="ShotZone"/>.</summary>
    public static ShotZone Classify(int x, int y)
    {
        if (x < 0)
        {
            return ShotZone.Backcourt;
        }

        var distance = DistanceFromHoop(x, y);

        // Corner three: outside the corner line, at or past the break point. Checked before the arc
        // because the corner line is a straight cut, not part of the circle.
        if (Math.Abs(y) > CornerThreeY)
        {
            return x >= CornerBreakX ? ShotZone.CornerThree : ShotZone.AboveBreakThree;
        }

        if (distance > ThreePointRadiusCm)
        {
            return ShotZone.AboveBreakThree;
        }

        if (distance <= RestrictedAreaRadiusCm)
        {
            return ShotZone.RestrictedArea;
        }

        // Inside the key: |y| within the paint and at/beyond the free-throw line.
        if (Math.Abs(y) <= PaintHalfWidthCm && x >= FreeThrowLineX)
        {
            return ShotZone.Paint;
        }

        return ShotZone.MidRange;
    }

    /// <summary>True when the zone is worth three points.</summary>
    public static bool IsThreePointZone(ShotZone zone)
        => zone is ShotZone.CornerThree or ShotZone.AboveBreakThree;
}
