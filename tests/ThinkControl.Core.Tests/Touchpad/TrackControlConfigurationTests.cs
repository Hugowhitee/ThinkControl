using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class TrackControlConfigurationTests
{
    [Fact]
    public void TrackActionEnablesIntegratedPlayPauseByDefault()
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
        Assert.False(sanitized.TrackCenterPlayPauseDisabled);
        Assert.Equal(GestureActionKind.PreviousNextTrack, sanitized.BindingFor(TouchpadEdge.Bottom).Action);
    }

    [Fact]
    public void TrackPlayPauseCanBeExplicitlyDisabledWithoutRemovingTrack()
    {
        var configuration = new TouchpadGestureConfiguration(
            TrackCenterPlayPauseEnabled: true,
            TrackCenterPlayPauseDisabled: true,
            Bindings: new TouchpadGestureBindings(
                Left: new TouchpadEdgeBinding(GestureActionKind.Volume),
                Right: new TouchpadEdgeBinding(GestureActionKind.Brightness),
                Top: new TouchpadEdgeBinding(GestureActionKind.MediaSeek),
                Bottom: new TouchpadEdgeBinding(GestureActionKind.PreviousNextTrack)));

        TouchpadGestureConfiguration sanitized = configuration.Sanitize();

        Assert.False(sanitized.TrackCenterPlayPauseEnabled);
        Assert.True(sanitized.TrackCenterPlayPauseDisabled);
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
    public void TrackPlayPausePreferenceSurvivesRemovingAndReaddingTrack()
    {
        var withoutTrack = new TouchpadGestureConfiguration(
            TrackCenterPlayPauseDisabled: true,
            Bindings: TouchpadGestureBindings.AsusStyle).Sanitize();

        Assert.False(withoutTrack.TrackCenterPlayPauseEnabled);
        Assert.True(withoutTrack.TrackCenterPlayPauseDisabled);

        TouchpadGestureConfiguration withTrack = (withoutTrack with
        {
            Bindings = new TouchpadGestureBindings(
                Left: new TouchpadEdgeBinding(GestureActionKind.Volume),
                Right: new TouchpadEdgeBinding(GestureActionKind.Brightness),
                Top: new TouchpadEdgeBinding(GestureActionKind.MediaSeek),
                Bottom: new TouchpadEdgeBinding(GestureActionKind.PreviousNextTrack))
        }).Sanitize();

        Assert.False(withTrack.TrackCenterPlayPauseEnabled);
        Assert.True(withTrack.TrackCenterPlayPauseDisabled);
    }

    [Fact]
    public void LegacyStandalonePlayPauseMigratesIntoTrackControlWithCenterEnabled()
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
