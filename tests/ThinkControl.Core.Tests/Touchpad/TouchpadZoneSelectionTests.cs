using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class TouchpadZoneSelectionTests
{
    [Fact]
    public void SelectingCorner_ClearsEdgeSelection()
    {
        TouchpadZoneSelection selection = TouchpadZoneSelection.ForEdge(TouchpadEdge.Left)
            .SelectCorner(TouchpadCorner.TopRight);

        Assert.True(selection.IsCorner);
        Assert.Equal(TouchpadCorner.TopRight, selection.Corner);
        Assert.Null(selection.Edge);
    }

    [Fact]
    public void SelectingEdge_ClearsCornerSelection()
    {
        TouchpadZoneSelection selection = TouchpadZoneSelection.ForCorner(TouchpadCorner.TopLeft)
            .SelectEdge(TouchpadEdge.Bottom);

        Assert.True(selection.IsEdge);
        Assert.Equal(TouchpadEdge.Bottom, selection.Edge);
        Assert.Null(selection.Corner);
    }

    [Fact]
    public void InvalidSelection_SanitizesToTopEdge()
    {
        var invalid = new TouchpadZoneSelection(TouchpadEdge.Left, TouchpadCorner.TopLeft);

        TouchpadZoneSelection selection = invalid.Sanitize();

        Assert.True(selection.IsEdge);
        Assert.Equal(TouchpadEdge.Top, selection.Edge);
        Assert.Null(selection.Corner);
    }
}
