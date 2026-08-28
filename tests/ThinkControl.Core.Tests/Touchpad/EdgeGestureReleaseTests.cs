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
        Assert.InRange(released?.EdgePosition01 ?? -1, 0.45, 0.47);
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

        GestureSignal? candidate = recognizer.ProcessFrame([new TouchContact(1, 6750, 120, true)], Geometry);
        GestureSignal? released = recognizer.ProcessFrame([]);

        Assert.Equal(GesturePhase.Candidate, candidate?.Phase);
        Assert.Equal(GestureActionKind.PreviousNextTrack, candidate?.Action);
        Assert.Equal(GesturePhase.Released, released?.Phase);
        Assert.Equal(GestureActionKind.PreviousNextTrack, released?.Action);
        Assert.Equal(0, released?.TotalTravelMm);
        Assert.InRange(released?.EdgePosition01 ?? -1, 0.499, 0.501);
    }

    [Fact]
    public void StationaryTrackCandidate_PreservesOffCenterStartForSafetyGate()
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

        recognizer.ProcessFrame([new TouchContact(1, 2600, 120, true)], Geometry);
        GestureSignal? released = recognizer.ProcessFrame([]);

        Assert.Equal(GesturePhase.Released, released?.Phase);
        Assert.InRange(released?.EdgePosition01 ?? -1, 0.19, 0.20);
        Assert.False(TrackCenterGesturePolicy.IsInsideCenterZone(released?.EdgePosition01));
    }

    [Fact]
    public void MovingTrackCandidate_PreservesPreClaimTravelForCenterHoldGuard()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            TrackCenterPlayPauseEnabled = true,
            ActivationDistanceMm = 4,
            Bindings = new TouchpadGestureBindings(
                new(GestureActionKind.Volume),
                new(GestureActionKind.Brightness),
                new(GestureActionKind.PreviousNextTrack),
                new(GestureActionKind.Disabled))
        };
        var recognizer = new EdgeGestureRecognizer(config);

        recognizer.ProcessFrame([new TouchContact(1, 6200, 120, true)], Geometry);
        recognizer.ProcessFrame([new TouchContact(1, 6450, 120, true)]);
        GestureSignal? released = recognizer.ProcessFrame([]);

        Assert.Equal(GesturePhase.Released, released?.Phase);
        Assert.InRange(released?.TotalTravelMm ?? 0, 2.4, 2.6);
    }

    [Fact]
    public void TopLeftCornerLaunch_RequiresDeliberateDiagonalInwardMotion()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            CornerLaunches = new TouchpadCornerLaunchBindings(
                TopLeft: GestureActionKind.OpenThinkControl,
                TopRight: GestureActionKind.Disabled)
        };
        var recognizer = new EdgeGestureRecognizer(config);

        GestureSignal? candidate = recognizer.ProcessFrame([new TouchContact(1, 600, 500, true)], Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([new TouchContact(1, 1250, 1150, true)]);

        Assert.Equal(GesturePhase.Candidate, candidate?.Phase);
        Assert.Equal(TouchpadCorner.TopLeft, candidate?.Corner);
        Assert.Equal(GestureActionKind.OpenThinkControl, candidate?.Action);
        Assert.Equal(GesturePhase.Claimed, claimed?.Phase);
        Assert.Equal(TouchpadCorner.TopLeft, claimed?.Corner);
        Assert.Null(claimed?.Edge);
        Assert.Equal(GestureActionKind.OpenThinkControl, claimed?.Action);
    }

    [Fact]
    public void TopRightCornerLaunch_CanOpenAdvanced()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            CornerLaunches = new TouchpadCornerLaunchBindings(
                TopLeft: GestureActionKind.Disabled,
                TopRight: GestureActionKind.OpenAdvanced)
        };
        var recognizer = new EdgeGestureRecognizer(config);

        recognizer.ProcessFrame([new TouchContact(1, 12900, 500, true)], Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([new TouchContact(1, 12200, 1200, true)]);

        Assert.Equal(GesturePhase.Claimed, claimed?.Phase);
        Assert.Equal(TouchpadCorner.TopRight, claimed?.Corner);
        Assert.Equal(GestureActionKind.OpenAdvanced, claimed?.Action);
    }

    [Fact]
    public void CornerTap_DoesNotLaunchAnything()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            CornerLaunches = new TouchpadCornerLaunchBindings(
                TopLeft: GestureActionKind.OpenThinkControl)
        };
        var recognizer = new EdgeGestureRecognizer(config);

        GestureSignal? candidate = recognizer.ProcessFrame([new TouchContact(1, 600, 500, true)], Geometry);
        GestureSignal? released = recognizer.ProcessFrame([]);

        Assert.Equal(GesturePhase.Candidate, candidate?.Phase);
        Assert.Equal(TouchpadCorner.TopLeft, candidate?.Corner);
        Assert.Null(released);
    }

    [Fact]
    public void CornerVerticalScroll_DoesNotLaunch()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            CornerLaunches = new TouchpadCornerLaunchBindings(
                TopLeft: GestureActionKind.OpenThinkControl)
        };
        var recognizer = new EdgeGestureRecognizer(config);

        recognizer.ProcessFrame([new TouchContact(1, 600, 500, true)], Geometry);
        GestureSignal? first = recognizer.ProcessFrame([new TouchContact(1, 600, 1400, true)]);
        GestureSignal? rejected = recognizer.ProcessFrame([new TouchContact(1, 600, 1800, true)]);

        Assert.Null(first);
        Assert.Equal(GesturePhase.Cancelled, rejected?.Phase);
        Assert.Equal(TouchpadCorner.TopLeft, rejected?.Corner);
        Assert.NotEqual(GesturePhase.Claimed, rejected?.Phase);
    }

    [Fact]
    public void EdgeLaunchActions_AreMigratedOffPrecisionEdges()
    {
        var bindings = new TouchpadGestureBindings(
            new(GestureActionKind.OpenThinkControl),
            new(GestureActionKind.OpenAdvanced),
            new(GestureActionKind.MediaSeek),
            new(GestureActionKind.Disabled)).Sanitize();

        Assert.Equal(GestureActionKind.Disabled, bindings.Left?.Action);
        Assert.Equal(GestureActionKind.Disabled, bindings.Right?.Action);
        Assert.Equal(GestureActionKind.MediaSeek, bindings.Top?.Action);
    }
}