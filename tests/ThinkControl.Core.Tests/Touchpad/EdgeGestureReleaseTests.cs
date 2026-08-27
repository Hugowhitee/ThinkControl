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

    [Fact]
    public void StationaryTrackCandidate_EmitsReleaseSoCenterHoldCanCommit()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            TrackCenterPlayPauseEnabled = true,
            Bindings = new TouchpadGestureBindings(
                new(GestureActionKind.Volume),
                new(GestureActionKind.Brightness),
                new(GestureActionKind.PreviousNextTrack),
                new(GestureActionKind.Disabled))
        };
        var recognizer = new EdgeGestureRecognizer(config);

        GestureSignal? candidate = recognizer.ProcessFrame([new TouchContact(1, 6200, 120, true)], Geometry);
        GestureSignal? released = recognizer.ProcessFrame([]);

        Assert.Equal(GesturePhase.Candidate, candidate?.Phase);
        Assert.Equal(GestureActionKind.PreviousNextTrack, candidate?.Action);
        Assert.Equal(GesturePhase.Released, released?.Phase);
        Assert.Equal(GestureActionKind.PreviousNextTrack, released?.Action);
        Assert.Equal(0, released?.TotalTravelMm);
    }

    [Fact]
    public void OpenThinkControl_ClaimsOnlyOnInwardMotion()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            Bindings = new TouchpadGestureBindings(
                new(GestureActionKind.Volume),
                new(GestureActionKind.OpenThinkControl),
                new(GestureActionKind.MediaSeek),
                new(GestureActionKind.Disabled))
        };
        var recognizer = new EdgeGestureRecognizer(config);

        recognizer.ProcessFrame([new TouchContact(1, 13380, 4000, true)], Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([new TouchContact(1, 12900, 4000, true)]);

        Assert.Equal(GesturePhase.Claimed, claimed?.Phase);
        Assert.Equal(TouchpadEdge.Right, claimed?.Edge);
        Assert.Equal(GestureActionKind.OpenThinkControl, claimed?.Action);
        Assert.True(claimed?.TotalTravelMm > 4);
    }

    [Fact]
    public void OpenThinkControl_DoesNotStealMovementWhenItsEdgeWasNotCandidate()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            Bindings = new TouchpadGestureBindings(
                new(GestureActionKind.OpenThinkControl),
                new(GestureActionKind.Brightness),
                new(GestureActionKind.MediaSeek),
                new(GestureActionKind.Disabled))
        };
        var recognizer = new EdgeGestureRecognizer(config);

        // Start in the middle of the top edge: Left is not a candidate at all.
        recognizer.ProcessFrame([new TouchContact(1, 6200, 120, true)], Geometry);
        GestureSignal? signal = recognizer.ProcessFrame([new TouchContact(1, 6600, 120, true)]);

        Assert.NotEqual(GestureActionKind.OpenThinkControl, signal?.Action);
        Assert.Equal(TouchpadEdge.Top, signal?.Edge);
        Assert.Equal(GestureActionKind.MediaSeek, signal?.Action);
    }
}
