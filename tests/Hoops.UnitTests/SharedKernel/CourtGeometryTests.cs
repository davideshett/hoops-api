using FluentAssertions;
using Hoops.SharedKernel;

namespace Hoops.UnitTests.SharedKernel;

public sealed class CourtGeometryTests
{
    [Fact]
    public void The_corner_break_point_matches_the_fiba_formula()
    {
        // §8 gives the formula 1242.5 − √(675² − 660²) and rounds it to "≈ 1099"; evaluated exactly it
        // is 1100.99. We follow the formula, which is unambiguous, rather than the rounded figure.
        var expected = 1242.5 - Math.Sqrt((675.0 * 675.0) - (660.0 * 660.0));

        CourtGeometry.CornerBreakX.Should().BeApproximately(expected, 0.001);
        CourtGeometry.CornerBreakX.Should().BeApproximately(1100.99, 0.01);
    }

    [Fact]
    public void Every_point_inside_the_arc_classifies_as_two_and_outside_as_three()
    {
        // Property sweep across the attacking half, avoiding the boundary itself.
        for (var x = 0; x <= CourtGeometry.HalfLengthCm; x += 25)
        {
            for (var y = -CourtGeometry.HalfWidthCm; y <= CourtGeometry.HalfWidthCm; y += 25)
            {
                var zone = CourtGeometry.Classify(x, y);
                var distance = CourtGeometry.DistanceFromHoop(x, y);
                var inCorner = Math.Abs(y) > CourtGeometry.CornerThreeY && x >= CourtGeometry.CornerBreakX;

                if (CourtGeometry.IsThreePointZone(zone))
                {
                    (distance > CourtGeometry.ThreePointRadiusCm || inCorner).Should()
                        .BeTrue("({0},{1}) classified {2} must be beyond the arc or in the corner", x, y, zone);
                }
                else
                {
                    distance.Should().BeLessThanOrEqualTo(CourtGeometry.ThreePointRadiusCm,
                        "({0},{1}) classified {2} must be inside the arc", x, y, zone);
                }
            }
        }
    }

    [Theory]
    [InlineData(1242, 0, ShotZone.RestrictedArea)]        // at the rim
    [InlineData(1150, 0, ShotZone.RestrictedArea)]        // 92.5 cm from the hoop
    [InlineData(1000, 100, ShotZone.Paint)]               // in the key, outside the restricted area
    [InlineData(900, 400, ShotZone.MidRange)]             // outside the paint, inside the arc
    [InlineData(500, 0, ShotZone.AboveBreakThree)]        // 742.5 cm out, top of the key
    [InlineData(1200, 700, ShotZone.CornerThree)]         // deep corner, past the break point
    [InlineData(-500, 0, ShotZone.Backcourt)]             // own half
    public void Landmark_points_classify_as_expected(int x, int y, ShotZone expected)
        => CourtGeometry.Classify(x, y).Should().Be(expected);

    [Fact]
    public void A_shot_just_outside_the_corner_line_but_short_of_the_break_is_above_the_break()
    {
        // |y| beyond the corner line but nearer half-way than the break point: the arc, not the corner.
        CourtGeometry.Classify(900, 700).Should().Be(ShotZone.AboveBreakThree);
    }

    [Fact]
    public void Bounds_checking_rejects_points_off_the_court()
    {
        CourtGeometry.IsInBounds(0, 0).Should().BeTrue();
        CourtGeometry.IsInBounds(1400, 750).Should().BeTrue();
        CourtGeometry.IsInBounds(1401, 0).Should().BeFalse();
        CourtGeometry.IsInBounds(0, -751).Should().BeFalse();
    }

    [Fact]
    public void Distance_from_the_hoop_is_measured_from_the_fiba_hoop_centre()
        => CourtGeometry.DistanceFromHoop(1242, 0).Should().BeApproximately(0.5, 0.6);
}
