using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class EdgeGestureRecognizerTests
{
    private static readonly TouchpadGeometry X9Geometry = new(
        0, 13500,
        0, 8000,
        135,
        80);

    [Fact]
    public void ContactAwayFromEdge_DoesNotStartGesture()
    {
        var recognizer = Create();
        GestureSignal? signal = recognizer.ProcessFrame([Contact(1, 6750, 4000)], X9Geometry);
        Assert.Null(signal);
        Assert.False(recognizer.HasCandidateOrActiveGesture);
    }

    [Fact]
    public void ContactStartedAwayFromEdge_CannotBecomeGestureUntilLift()
    {
        var recognizer = Create();
        Assert.Null(recognizer.ProcessFrame([Contact(1, 6750, 4000)], X9Geometry));
        Assert.Null(recognizer.ProcessFrame([Contact(1, 13300, 4000)]));
        Assert.Null(recognizer.ProcessFrame([Contact(1, 13300, 3600)]));

        recognizer.ProcessFrame([]);
        GestureSignal? afterLift = recognizer.ProcessFrame([Contact(2, 13300, 4000)]);
        Assert.Equal(GesturePhase.Candidate, afterLift?.Phase);
    }

    [Fact]
    public void RightEdgeVerticalMovement_ClaimsBrightness()
    {
        var recognizer = Create();
        GestureSignal? candidate = recognizer.ProcessFrame([Contact(1, 13300, 5000)], X9Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([Contact(1, 13300, 4700)]);
        Assert.Equal(GesturePhase.Candidate, candidate?.Phase);
        Assert.Equal(GesturePhase.Claimed, claimed?.Phase);
        Assert.Equal(TouchpadEdge.Right, claimed?.Edge);
        Assert.Equal(GestureActionKind.Brightness, claimed?.Action);
        Assert.True(claimed?.TotalTravelMm < 0);
    }

    [Fact]
    public void RightEdgeHorizontalMovement_CancelsCandidate()
    {
        var recognizer = Create();
        recognizer.ProcessFrame([Contact(1, 13300, 5000)], X9Geometry);
        GestureSignal? cancelled = recognizer.ProcessFrame([Contact(1, 12900, 5000)]);
        Assert.Equal(GesturePhase.Cancelled, cancelled?.Phase);
        Assert.Equal("Wrong direction", cancelled?.Reason);
        Assert.False(recognizer.HasCandidateOrActiveGesture);
    }

    [Fact]
    public void TopEdgeHorizontalMovement_ClaimsMediaSeek()
    {
        var recognizer = Create();
        recognizer.ProcessFrame([Contact(1, 6500, 150)], X9Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([Contact(1, 6850, 150)]);
        Assert.Equal(GesturePhase.Claimed, claimed?.Phase);
        Assert.Equal(TouchpadEdge.Top, claimed?.Edge);
        Assert.Equal(GestureActionKind.MediaSeek, claimed?.Action);
    }

    [Fact]
    public void TopRightCorner_HorizontalMovementSelectsTop()
    {
        var recognizer = Create();
        GestureSignal? candidate = recognizer.ProcessFrame([Contact(1, 13300, 150)], X9Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([Contact(1, 12950, 150)]);
        Assert.Equal(GesturePhase.Candidate, candidate?.Phase);
        Assert.Null(candidate?.Edge);
        Assert.Equal(TouchpadEdge.Top, claimed?.Edge);
        Assert.Equal(GestureActionKind.MediaSeek, claimed?.Action);
    }

    [Fact]
    public void TopRightCorner_VerticalMovementSelectsRight()
    {
        var recognizer = Create();
        recognizer.ProcessFrame([Contact(1, 13300, 150)], X9Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([Contact(1, 13300, 450)]);
        Assert.Equal(TouchpadEdge.Right, claimed?.Edge);
        Assert.Equal(GestureActionKind.Brightness, claimed?.Action);
    }

    [Fact]
    public void ConfiguredCornerOwnsOverlapFromFirstCandidateFrame()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            CornerLaunches = new TouchpadCornerLaunchBindings(
                TopLeft: GestureActionKind.OpenThinkControl,
                TopRight: GestureActionKind.Disabled)
        };
        var recognizer = new EdgeGestureRecognizer(config);

        // 6 x 5 mm is inside the visible diagonal corner lane and also close enough
        // to the top edge to be ambiguous without explicit first-frame ownership.
        GestureSignal? candidate = recognizer.ProcessFrame([Contact(1, 600, 500)], X9Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([Contact(1, 1300, 1200)]);

        Assert.Equal(GesturePhase.Candidate, candidate?.Phase);
        Assert.Equal(TouchpadCorner.TopLeft, candidate?.Corner);
        Assert.Null(candidate?.Edge);
        Assert.Equal(GesturePhase.Claimed, claimed?.Phase);
        Assert.Equal(TouchpadCorner.TopLeft, claimed?.Corner);
        Assert.Null(claimed?.Edge);
        Assert.Equal(GestureActionKind.OpenThinkControl, claimed?.Action);
    }

    [Fact]
    public void CornerCandidateNeverFallsThroughToEdgeBeforeLift()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            CornerLaunches = new TouchpadCornerLaunchBindings(
                TopLeft: GestureActionKind.OpenAdvanced,
                TopRight: GestureActionKind.Disabled)
        };
        var recognizer = new EdgeGestureRecognizer(config);

        GestureSignal? candidate = recognizer.ProcessFrame([Contact(1, 600, 500)], X9Geometry);
        GestureSignal? cancelled = recognizer.ProcessFrame([Contact(1, 1500, 500)]);
        GestureSignal? whileStillDown = recognizer.ProcessFrame([Contact(1, 2200, 450)]);

        Assert.Equal(TouchpadCorner.TopLeft, candidate?.Corner);
        Assert.Null(candidate?.Edge);
        Assert.Equal(GesturePhase.Cancelled, cancelled?.Phase);
        Assert.Null(cancelled?.Edge);
        Assert.Null(whileStillDown);

        recognizer.ProcessFrame([]);
        GestureSignal? nextContact = recognizer.ProcessFrame([Contact(2, 6500, 150)]);
        Assert.Equal(GesturePhase.Candidate, nextContact?.Phase);
        Assert.Null(nextContact?.Corner);
    }

    [Fact]
    public void MirroredRightCornerAlsoOwnsEdgeOverlap()
    {
        var config = TouchpadGestureConfiguration.Default with
        {
            CornerLaunches = new TouchpadCornerLaunchBindings(
                TopLeft: GestureActionKind.Disabled,
                TopRight: GestureActionKind.OpenAdvanced)
        };
        var recognizer = new EdgeGestureRecognizer(config);

        GestureSignal? candidate = recognizer.ProcessFrame([Contact(1, 12900, 500)], X9Geometry);
        GestureSignal? claimed = recognizer.ProcessFrame([Contact(1, 12200, 1200)]);

        Assert.Equal(TouchpadCorner.TopRight, candidate?.Corner);
        Assert.Null(candidate?.Edge);
        Assert.Equal(TouchpadCorner.TopRight, claimed?.Corner);
        Assert.Null(claimed?.Edge);
        Assert.Equal(GestureActionKind.OpenAdvanced, claimed?.Action);
    }

    [Fact]
    public void SecondFinger_CancelsAndLocksOutUntilAllLift()
    {
        var recognizer = Create();
        recognizer.ProcessFrame([Contact(1, 13300, 5000)], X9Geometry);
        recognizer.ProcessFrame([Contact(1, 13300, 4700)]);
        GestureSignal? cancelled = recognizer.ProcessFrame([
            Contact(1, 13300, 4600),
            Contact(2, 7000, 4000)]);
        GestureSignal? whileLocked = recognizer.ProcessFrame([Contact(1, 13300, 4500)]);
        recognizer.ProcessFrame([]);
        GestureSignal? afterLift = recognizer.ProcessFrame([Contact(3, 13300, 5000)]);
        Assert.Equal(GesturePhase.Cancelled, cancelled?.Phase);
        Assert.Equal("Second finger detected", cancelled?.Reason);
        Assert.Null(whileLocked);
        Assert.Equal(GesturePhase.Candidate, afterLift?.Phase);
    }

    [Fact]
    public void LowConfidenceContact_NeverStartsGesture()
    {
        var recognizer = Create();
        GestureSignal? signal = recognizer.ProcessFrame([
            Contact(1, 13300, 5000) with { Confidence = false }
        ], X9Geometry);
        Assert.Null(signal);
        Assert.False(recognizer.HasCandidateOrActiveGesture);
    }

    [Fact]
    public void LowConfidenceDuringActiveGesture_Cancels()
    {
        var recognizer = Create();
        recognizer.ProcessFrame([Contact(1, 13300, 5000)], X9Geometry);
        recognizer.ProcessFrame([Contact(1, 13300, 4700)]);
        GestureSignal? cancelled = recognizer.ProcessFrame([
            Contact(1, 13300, 4500) with { Confidence = false }
        ]);
        Assert.Equal(GesturePhase.Cancelled, cancelled?.Phase);
        Assert.Equal("Low-confidence contact", cancelled?.Reason);
    }

    [Fact]
    public void ActiveGesture_CancelsWhenFingerLeavesContinuationCorridor()
    {
        var recognizer = Create();
        recognizer.ProcessFrame([Contact(1, 13300, 5000)], X9Geometry);
        recognizer.ProcessFrame([Contact(1, 13300, 4700)]);
        GestureSignal? cancelled = recognizer.ProcessFrame([Contact(1, 11500, 4400)]);
        Assert.Equal(GesturePhase.Cancelled, cancelled?.Phase);
        Assert.Equal("Gesture left edge tolerance", cancelled?.Reason);
    }

    [Fact]
    public void FingerLift_ReleasesClaimedGesture()
    {
        var recognizer = Create();
        recognizer.ProcessFrame([Contact(1, 13300, 5000)], X9Geometry);
        recognizer.ProcessFrame([Contact(1, 13300, 4700)]);
        GestureSignal? released = recognizer.ProcessFrame([]);
        Assert.Equal(GesturePhase.Released, released?.Phase);
        Assert.Equal(TouchpadEdge.Right, released?.Edge);
        Assert.Equal(GestureActionKind.Brightness, released?.Action);
        Assert.False(recognizer.HasCandidateOrActiveGesture);
    }

    [Fact]
    public void DisabledConfiguration_DropsExistingCandidate()
    {
        var recognizer = Create();
        recognizer.ProcessFrame([Contact(1, 13300, 5000)], X9Geometry);
        recognizer.SetConfiguration(TouchpadGestureConfiguration.Default with { Enabled = false });
        GestureSignal? signal = recognizer.ProcessFrame([Contact(1, 13300, 4700)]);
        Assert.Null(signal);
        Assert.False(recognizer.HasCandidateOrActiveGesture);
    }

    private static EdgeGestureRecognizer Create() =>
        new(TouchpadGestureConfiguration.Default);

    private static TouchContact Contact(int id, int x, int y) =>
        new(id, x, y, true, true);
}
