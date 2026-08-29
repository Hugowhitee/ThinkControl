using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class TouchpadGestureConfigurationTests
{
    [Fact]
    public void Sanitize_RemovesRetiredActionsAndFoldsMuteIntoVolume()
    {
        var configuration = new TouchpadGestureConfiguration(Bindings: new TouchpadGestureBindings(
            new(GestureActionKind.Mute),
            new(GestureActionKind.TaskView),
            new(GestureActionKind.ShowDesktop),
            new(GestureActionKind.PerformanceMode)));

        TouchpadGestureBindings bindings = configuration.Sanitize().Bindings!;

        Assert.Equal(GestureActionKind.Volume, bindings.Get(TouchpadEdge.Left).Action);
        Assert.Equal(GestureActionKind.Disabled, bindings.Get(TouchpadEdge.Right).Action);
        Assert.Equal(GestureActionKind.Disabled, bindings.Get(TouchpadEdge.Top).Action);
        Assert.Equal(GestureActionKind.Disabled, bindings.Get(TouchpadEdge.Bottom).Action);
    }

    [Fact]
    public void Sanitize_KeepsOnlyOnePhysicalAssignmentPerAction()
    {
        var configuration = new TouchpadGestureConfiguration(Bindings: new TouchpadGestureBindings(
            new(GestureActionKind.Brightness),
            new(GestureActionKind.Brightness),
            new(GestureActionKind.MediaSeek),
            new(GestureActionKind.MediaSeek)));

        TouchpadGestureBindings bindings = configuration.Sanitize().Bindings!;

        Assert.Equal(GestureActionKind.Brightness, bindings.Get(TouchpadEdge.Left).Action);
        Assert.Equal(GestureActionKind.Disabled, bindings.Get(TouchpadEdge.Right).Action);
        Assert.Equal(GestureActionKind.MediaSeek, bindings.Get(TouchpadEdge.Top).Action);
        Assert.Equal(GestureActionKind.Disabled, bindings.Get(TouchpadEdge.Bottom).Action);
    }

    [Fact]
    public void ReverseClose_IsEffectiveOnlyForAnEnabledCornerLaunch()
    {
        var configuration = new TouchpadGestureConfiguration(
            CornerLaunches: new TouchpadCornerLaunchBindings(
                TopLeft: GestureActionKind.OpenThinkControl,
                TopRight: GestureActionKind.Disabled,
                TopLeftReverseClose: true,
                TopRightReverseClose: true));

        Assert.True(configuration.ReverseCloseFor(TouchpadCorner.TopLeft));
        Assert.False(configuration.ReverseCloseFor(TouchpadCorner.TopRight));
    }
}
