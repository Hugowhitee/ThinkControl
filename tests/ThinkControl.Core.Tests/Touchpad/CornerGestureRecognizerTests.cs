using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class CornerGestureRecognizerTests
{
    private static readonly TouchpadGeometry Geometry = new(0, 13500, 0, 8000, 135, 80);

    [Fact]
    public void GuardStart_OwnsSideEdgeOverlapBeforeDirectionIsKnown()
    {
        EdgeGestureRecognizer recognizer = Create(
            TouchpadCorner.TopLeft,
            GestureActionKind.OpenThinkControl,
            reverseClose: false);

        GestureSignal? candidate = recognizer.ProcessFrame([Contact(1, 100, 800)], Geometry);
        GestureSignal? cancelled = recognizer.ProcessFrame([Contact(1, 100, 1900)]);
        GestureSignal? stillDown = recognizer.ProcessFrame([Contact(1, 100, 2600)]);

        Assert.Equal(TouchpadCorner.TopLeft, candidate?.Corner);
        Assert.Null(candidate?.Edge);
        Assert.Equal(GesturePhase.Cancelled, cancelled?.Phase);
        Assert.Null(cancelled?.Edge);
        Assert.Null(stillDown);
    }

    [Fact]
    public void ReverseClose_LeftCorner_ClaimsDeliberateOutwardDiagonal()
    {
        EdgeGestureRecognizer recognizer = Create(
            TouchpadCorner.TopLeft,
            GestureActionKind.OpenThinkControl,
            reverseClose: true);

        GestureSignal? candidate = recognizer.ProcessFrame([Contact(1, 1650, 1650)], Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([Contact(1, 950, 950)]);

        Assert.Equal(CornerGestureDirection.Outward, candidate?.CornerDirection);
        Assert.Equal(GesturePhase.Claimed, claimed?.Phase);
        Assert.Equal(TouchpadCorner.TopLeft, claimed?.Corner);
        Assert.Equal(CornerGestureDirection.Outward, claimed?.CornerDirection);
        Assert.Equal(GestureActionKind.OpenThinkControl, claimed?.Action);
    }

    [Fact]
    public void ReverseClose_RightCorner_IsExactDirectionalMirror()
    {
        EdgeGestureRecognizer recognizer = Create(
            TouchpadCorner.TopRight,
            GestureActionKind.OpenAdvanced,
            reverseClose: true);

        GestureSignal? candidate = recognizer.ProcessFrame([Contact(1, 13500 - 1650, 1650)], Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([Contact(1, 13500 - 950, 950)]);

        Assert.Equal(CornerGestureDirection.Outward, candidate?.CornerDirection);
        Assert.Equal(GesturePhase.Claimed, claimed?.Phase);
        Assert.Equal(TouchpadCorner.TopRight, claimed?.Corner);
        Assert.Equal(CornerGestureDirection.Outward, claimed?.CornerDirection);
        Assert.Equal(GestureActionKind.OpenAdvanced, claimed?.Action);
    }

    [Fact]
    public void ReverseClose_WrongDirectionRejectsAndNeverFallsThroughToEdge()
    {
        EdgeGestureRecognizer recognizer = Create(
            TouchpadCorner.TopLeft,
            GestureActionKind.OpenThinkControl,
            reverseClose: true);

        GestureSignal? candidate = recognizer.ProcessFrame([Contact(1, 1650, 1650)], Geometry);
        GestureSignal? cancelled = recognizer.ProcessFrame([Contact(1, 2350, 2350)]);
        GestureSignal? stillDown = recognizer.ProcessFrame([Contact(1, 2600, 2600)]);

        Assert.Equal(CornerGestureDirection.Outward, candidate?.CornerDirection);
        Assert.Equal(GesturePhase.Cancelled, cancelled?.Phase);
        Assert.Equal("Reverse corner gesture moved inward", cancelled?.Reason);
        Assert.Null(stillDown);
    }

    private static EdgeGestureRecognizer Create(
        TouchpadCorner corner,
        GestureActionKind action,
        bool reverseClose)
    {
        TouchpadCornerLaunchBindings launches = corner == TouchpadCorner.TopLeft
            ? new TouchpadCornerLaunchBindings(
                TopLeft: action,
                TopLeftReverseClose: reverseClose)
            : new TouchpadCornerLaunchBindings(
                TopRight: action,
                TopRightReverseClose: reverseClose);
        return new EdgeGestureRecognizer(
            TouchpadGestureConfiguration.Default with { CornerLaunches = launches });
    }

    private static TouchContact Contact(int id, int x, int y) =>
        new(id, x, y, true, true);
}
