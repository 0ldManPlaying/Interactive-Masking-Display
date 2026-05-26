using InteractiveMask.Detection;
using InteractiveMask.Display;
using Xunit;

namespace InteractiveMask.Tests;

public sealed class PointInPolygonHelperTests
{
    [Fact]
    public void Contains_point_inside_square_returns_true()
    {
        var square = new List<PolygonPoint>
        {
            new(0, 0),
            new(10, 0),
            new(10, 10),
            new(0, 10),
        };

        Assert.True(PointInPolygonHelper.Contains(5, 5, square));
    }

    [Fact]
    public void Contains_point_outside_square_returns_false()
    {
        var square = new List<PolygonPoint>
        {
            new(0, 0),
            new(10, 0),
            new(10, 10),
            new(0, 10),
        };

        Assert.False(PointInPolygonHelper.Contains(15, 5, square));
    }

    [Fact]
    public void Contains_point_in_L_shaped_polygon_respects_hole_side()
    {
        var lShape = new List<PolygonPoint>
        {
            new(0, 0),
            new(10, 0),
            new(10, 5),
            new(5, 5),
            new(5, 10),
            new(0, 10),
        };

        Assert.True(PointInPolygonHelper.Contains(2, 2, lShape));
        Assert.False(PointInPolygonHelper.Contains(8, 8, lShape));
    }
}
