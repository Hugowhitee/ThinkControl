using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class TouchpadCornerZonePolicyTests
{
    private static readonly TouchpadGeometry Geometry = new(0, 13500, 0, 8000, 135, 80);

    [Theory]
    [InlineData(6.0, 5.0)]
    [InlineData(10.0, 10.0)]
    [InlineData(14.0, 12.0)]
    public void DiagonalLane_ContainsPointsAlongVisibleSwipeArea(double xMm, double yMm)
    {
        Assert.True(TouchpadCornerZonePolicy.ContainsLocal(xMm, yMm));
    }

    [Theory]
    [InlineData(1.0, 10.0)]
    [InlineData(10.0, 1.0)]
    [InlineData(18.0, 4.0)]
    [InlineData(20.0, 20.0)]
    public void DiagonalLane_RejectsOldSquareCornerAreaOutsideVisibleLane(double xMm, double yMm)
    {
        Assert.False(TouchpadCornerZonePolicy.ContainsLocal(xMm, yMm));
    }

    [Fact]
    public void Recognizer_DoesNotTreatInvisibleSquareAreaAsCornerLaunch()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            CornerLaunches = new TouchpadCornerLaunchBindings(
                TopLeft: GestureActionKind.OpenThinkControl,
                TopRight: GestureActionKind.Disabled)
        };
        var recognizer = new EdgeGestureRecognizer(config);

        // 1 mm from the left and 10 mm from the top was inside the old 13 x 13 mm
        // square, but it is visibly outside the new diagonal lane.
        GestureSignal? signal = recognizer.ProcessFrame([new TouchContact(1, 100, 1000, true)], Geometry);

        Assert.NotEqual(TouchpadCorner.TopLeft, signal?.Corner);
    }

    [Fact]
    public void RightLane_UsesMirroredPhysicalGeometry()
    {
        int x = 13500 - 600;
        int y = 500;

        Assert.True(TouchpadCornerZonePolicy.ContainsStart(TouchpadCorner.TopRight, Geometry, x, y));
        Assert.False(TouchpadCornerZonePolicy.ContainsStart(TouchpadCorner.TopLeft, Geometry, x, y));
    }

    [Theory]
    [InlineData(350, 300)]
    [InlineData(600, 500)]
    [InlineData(1000, 900)]
    [InlineData(1500, 1300)]
    [InlineData(1900, 1600)]
    public void LeftAndRightRecognitionLanes_AreExactLogicalMirrors(int leftX, int y)
    {
        int rightX = Geometry.XLogicalMax - (leftX - Geometry.XLogicalMin);

        bool left = TouchpadCornerZonePolicy.ContainsStart(TouchpadCorner.TopLeft, Geometry, leftX, y);
        bool right = TouchpadCornerZonePolicy.ContainsStart(TouchpadCorner.TopRight, Geometry, rightX, y);

        Assert.Equal(left, right);
    }
}
