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

        // Keep this generic swipe/release regression outside Track's dedicated
        // center hold reservation. Center-start behavior has its own tests below.
        recognizer.ProcessFrame([new TouchContact(1, 3500, 120, true)], Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([new TouchContact(1, 3850, 120, true)]);
        GestureSignal? active = recognizer.ProcessFrame([new TouchContact(1, 4900, 120, true)]);
        GestureSignal? released = recognizer.ProcessFrame([]);

        Assert.Equal(GestureActionKind.PreviousNextTrack, claimed?.Action);
        Assert.Equal(GesturePhase.Active, active?.Phase);
        Assert.True(active?.TotalTravelMm > 7);
        Assert.Equal(GesturePhase.Released, released?.Phase);
        Assert.Equal(active?.TotalTravelMm, released?.TotalTravelMm);
        Assert.InRange(released?.EdgePosition01 ?? -1, 0.25, 0.27);
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
    public void TrackCenterHold_AllowsSmallNaturalOffAxisDrift()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            Bindings = new TouchpadGestureBindings(
                Left: new(GestureActionKind.Volume),
                Right: new(GestureActionKind.Brightness),
                Top: new(GestureActionKind.MediaSeek),
                Bottom: new(GestureActionKind.PreviousNextTrack))
        };
        var recognizer = new EdgeGestureRecognizer(config);

        GestureSignal? candidate = recognizer.ProcessFrame([new TouchContact(1, 6750, 7880, true)], Geometry);
        GestureSignal? drift = recognizer.ProcessFrame([new TouchContact(1, 6900, 7700, true)]);
        GestureSignal? released = recognizer.ProcessFrame([]);

        Assert.Equal(GesturePhase.Candidate, candidate?.Phase);
        Assert.Null(drift);
        Assert.Equal(GesturePhase.Released, released?.Phase);
        Assert.Equal(GestureActionKind.PreviousNextTrack, released?.Action);
        Assert.InRange(released?.TotalTravelMm ?? -1, 2.3, 2.4);
        Assert.True(TrackCenterGesturePolicy.IsInsideCenterZone(released?.EdgePosition01));
    }

    [Fact]
    public void TrackCenterRelease_PreservesMaximumExcursionEvenIfFingerReturns()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            Bindings = new TouchpadGestureBindings(
                Left: new(GestureActionKind.Volume),
                Right: new(GestureActionKind.Brightness),
                Top: new(GestureActionKind.MediaSeek),
                Bottom: new(GestureActionKind.PreviousNextTrack))
        };
        var recognizer = new EdgeGestureRecognizer(config);

        recognizer.ProcessFrame([new TouchContact(1, 6750, 7880, true)], Geometry);
        Assert.Null(recognizer.ProcessFrame([new TouchContact(1, 7030, 7880, true)]));
        Assert.Null(recognizer.ProcessFrame([new TouchContact(1, 6800, 7880, true)]));
        GestureSignal? released = recognizer.ProcessFrame([]);

        Assert.Equal(GesturePhase.Released, released?.Phase);
        Assert.InRange(released?.TotalTravelMm ?? -1, 2.79, 2.81);
    }

    [Fact]
    public void TrackCenterDriftBeyondHoldEnvelope_ResumesNormalDirectionRejection()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            Bindings = new TouchpadGestureBindings(
                Left: new(GestureActionKind.Volume),
                Right: new(GestureActionKind.Brightness),
                Top: new(GestureActionKind.MediaSeek),
                Bottom: new(GestureActionKind.PreviousNextTrack))
        };
        var recognizer = new EdgeGestureRecognizer(config);

        recognizer.ProcessFrame([new TouchContact(1, 6750, 7880, true)], Geometry);
        GestureSignal? rejected = recognizer.ProcessFrame([new TouchContact(1, 6750, 7480, true)]);

        Assert.Equal(GesturePhase.Cancelled, rejected?.Phase);
        Assert.Equal(GestureActionKind.PreviousNextTrack, rejected?.Action);
        Assert.Equal("Wrong direction", rejected?.Reason);
        Assert.True((rejected?.TotalTravelMm ?? 0) > TrackCenterGesturePolicy.MovementToleranceMm);
    }

    [Fact]
    public void TrackCenterHorizontalSwipe_LeavesHoldReservationBeforeSkipScale()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            Bindings = new TouchpadGestureBindings(
                Left: new(GestureActionKind.Volume),
                Right: new(GestureActionKind.Brightness),
                Top: new(GestureActionKind.MediaSeek),
                Bottom: new(GestureActionKind.PreviousNextTrack))
        };
        var recognizer = new EdgeGestureRecognizer(config);

        recognizer.ProcessFrame([new TouchContact(1, 6750, 7880, true)], Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([new TouchContact(1, 7100, 7880, true)]);
        GestureSignal? active = recognizer.ProcessFrame([new TouchContact(1, 7700, 7880, true)]);

        Assert.Equal(GesturePhase.Claimed, claimed?.Phase);
        Assert.Equal(GestureActionKind.PreviousNextTrack, claimed?.Action);
        Assert.InRange(Math.Abs(claimed?.TotalTravelMm ?? 0), 3.49, 3.51);
        Assert.True(Math.Abs(claimed?.TotalTravelMm ?? 0) < TrackCenterGesturePolicy.SwipeThresholdMm);
        Assert.Equal(GesturePhase.Active, active?.Phase);
        Assert.True(Math.Abs(active?.TotalTravelMm ?? 0) >= TrackCenterGesturePolicy.SwipeThresholdMm);
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
            ActivationDistanceMm = 6,
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
