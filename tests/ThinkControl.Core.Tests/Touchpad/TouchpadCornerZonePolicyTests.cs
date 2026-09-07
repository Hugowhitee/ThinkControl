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
    [InlineData(1.0, 8.0)]
    [InlineData(5.0, 5.0)]
    [InlineData(8.0, 1.0)]
    public void EnabledCornerGuard_ContainsForgivingFirstFrameArea(double xMm, double yMm)
    {
        Assert.True(TouchpadCornerZonePolicy.ContainsLocal(xMm, yMm));
    }

    [Theory]
    [InlineData(1.0, 10.0)]
    [InlineData(10.0, 1.0)]
    [InlineData(18.0, 4.0)]
    [InlineData(20.0, 20.0)]
    public void CornerZone_RejectsAreaOutsideVisibleGuardAndLane(double xMm, double yMm)
    {
        Assert.False(TouchpadCornerZonePolicy.ContainsLocal(xMm, yMm));
    }

    [Fact]
    public void Recognizer_UsesVisibleGuardInsteadOfFallingThroughToEdge()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            CornerLaunches = new TouchpadCornerLaunchBindings(
                TopLeft: GestureActionKind.OpenThinkControl,
                TopRight: GestureActionKind.Disabled)
        };
        var recognizer = new EdgeGestureRecognizer(config);

        GestureSignal? signal = recognizer.ProcessFrame([new TouchContact(1, 100, 800, true)], Geometry);

        Assert.Equal(TouchpadCorner.TopLeft, signal?.Corner);
        Assert.Equal(CornerGestureDirection.Inward, signal?.CornerDirection);
        Assert.Null(signal?.Edge);
    }

    [Fact]
    public void ReverseEnabled_InnerHalfOfVisibleLaneOwnsOutwardStart()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            CornerLaunches = new TouchpadCornerLaunchBindings(
                TopLeft: GestureActionKind.OpenThinkControl,
                TopRight: GestureActionKind.Disabled,
                TopLeftReverseClose: true)
        };
        var recognizer = new EdgeGestureRecognizer(config);

        // 10 x 10 mm lies well before the rounded inner cap but inside the visible
        // inner half of the diagonal lane. Alpha.42 intentionally accepts it as a
        // reverse-close start so the user no longer has to hit an 8 mm cap precisely.
        GestureSignal? signal = recognizer.ProcessFrame([new TouchContact(1, 1000, 1000, true)], Geometry);

        Assert.True(TouchpadCornerZonePolicy.ContainsReverseStartLocal(10, 10));
        Assert.Equal(TouchpadCorner.TopLeft, signal?.Corner);
        Assert.Equal(CornerGestureDirection.Outward, signal?.CornerDirection);
    }

    [Fact]
    public void ReverseEnabled_OuterGuardStillBelongsToInwardLaunch()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            CornerLaunches = new TouchpadCornerLaunchBindings(
                TopLeft: GestureActionKind.OpenThinkControl,
                TopLeftReverseClose: true)
        };
        var recognizer = new EdgeGestureRecognizer(config);

        GestureSignal? signal = recognizer.ProcessFrame([new TouchContact(1, 500, 500, true)], Geometry);

        Assert.False(TouchpadCornerZonePolicy.ContainsReverseStartLocal(5, 5));
        Assert.Equal(CornerGestureDirection.Inward, signal?.CornerDirection);
    }

    [Fact]
    public void ReverseDisabled_InnerLaneRemainsAnInwardLaunchStart()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            CornerLaunches = new TouchpadCornerLaunchBindings(
                TopLeft: GestureActionKind.OpenAdvanced,
                TopRight: GestureActionKind.Disabled,
                TopLeftReverseClose: false)
        };
        var recognizer = new EdgeGestureRecognizer(config);

        GestureSignal? signal = recognizer.ProcessFrame([new TouchContact(1, 1000, 1000, true)], Geometry);

        Assert.Equal(CornerGestureDirection.Inward, signal?.CornerDirection);
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
    [InlineData(100, 800)]
    [InlineData(350, 300)]
    [InlineData(600, 500)]
    [InlineData(1000, 900)]
    [InlineData(1500, 1300)]
    [InlineData(1900, 1600)]
    public void LeftAndRightRecognitionZones_AreExactLogicalMirrors(int leftX, int y)
    {
        int rightX = Geometry.XLogicalMax - (leftX - Geometry.XLogicalMin);

        bool left = TouchpadCornerZonePolicy.ContainsStart(TouchpadCorner.TopLeft, Geometry, leftX, y);
        bool right = TouchpadCornerZonePolicy.ContainsStart(TouchpadCorner.TopRight, Geometry, rightX, y);

        Assert.Equal(left, right);
    }
}
