using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class EdgeGestureReleaseTests
{
    private static readonly TouchpadGeometry Geometry = new(0, 13500, 0, 8000, 135, 80);

    [Fact]
    public void ReleasedSignal_PreservesFinalTravelForDiscreteSwipeActions()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            Bindings = new TouchpadGestureBindings(
                new(GestureActionKind.Volume),
                new(GestureActionKind.Brightness),
                new(GestureActionKind.PreviousNextTrack),
                new(GestureActionKind.Disabled))
        };
        var recognizer = new EdgeGestureRecognizer(config);

        recognizer.ProcessFrame([new TouchContact(1, 6200, 120, true)], Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([new TouchContact(1, 6550, 120, true)]);
        GestureSignal? active = recognizer.ProcessFrame([new TouchContact(1, 7600, 120, true)]);
        GestureSignal? released = recognizer.ProcessFrame([]);

        Assert.Equal(GestureActionKind.PreviousNextTrack, claimed?.Action);
        Assert.Equal(GesturePhase.Active, active?.Phase);
        Assert.True(active?.TotalTravelMm > 7);
        Assert.Equal(GesturePhase.Released, released?.Phase);
        Assert.Equal(active?.TotalTravelMm, released?.TotalTravelMm);
    }
}
