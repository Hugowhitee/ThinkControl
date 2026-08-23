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
