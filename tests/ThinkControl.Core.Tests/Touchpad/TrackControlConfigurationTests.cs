using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class TrackControlConfigurationTests
{
    [Fact]
    public void TrackActionOwnsIntegratedPlayPauseSegment()
    {
        var configuration = new TouchpadGestureConfiguration(
            TrackCenterPlayPauseEnabled: false,
            Bindings: new TouchpadGestureBindings(
                Left: new TouchpadEdgeBinding(GestureActionKind.Volume),
                Right: new TouchpadEdgeBinding(GestureActionKind.Brightness),
                Top: new TouchpadEdgeBinding(GestureActionKind.MediaSeek),
                Bottom: new TouchpadEdgeBinding(GestureActionKind.PreviousNextTrack)));

        TouchpadGestureConfiguration sanitized = configuration.Sanitize();

        Assert.True(sanitized.TrackCenterPlayPauseEnabled);
        Assert.Equal(GestureActionKind.PreviousNextTrack, sanitized.BindingFor(TouchpadEdge.Bottom).Action);
    }

    [Fact]
    public void RemovingTrackActionRemovesIntegratedPlayPauseSegment()
    {
        var configuration = new TouchpadGestureConfiguration(
            TrackCenterPlayPauseEnabled: true,
            Bindings: TouchpadGestureBindings.AsusStyle);

        Assert.False(configuration.Sanitize().TrackCenterPlayPauseEnabled);
    }

    [Fact]
    public void LegacyStandalonePlayPauseMigratesIntoTrackControl()
    {
        var configuration = new TouchpadGestureConfiguration(
            Bindings: new TouchpadGestureBindings(
                Left: new TouchpadEdgeBinding(GestureActionKind.Volume),
                Right: new TouchpadEdgeBinding(GestureActionKind.Brightness),
                Top: new TouchpadEdgeBinding(GestureActionKind.PlayPause),
                Bottom: new TouchpadEdgeBinding(GestureActionKind.Disabled)));

        TouchpadGestureConfiguration sanitized = configuration.Sanitize();

        Assert.Equal(GestureActionKind.PreviousNextTrack, sanitized.BindingFor(TouchpadEdge.Top).Action);
        Assert.True(sanitized.TrackCenterPlayPauseEnabled);
    }
}
