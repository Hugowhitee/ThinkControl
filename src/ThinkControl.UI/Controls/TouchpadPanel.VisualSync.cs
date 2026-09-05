namespace ThinkControl.UI.Controls;

public partial class TouchpadPanel
{
    // Existing configuration call sites historically refreshed a separate gesture
    // overlay. The overlay no longer exists; keep those call sites pointed at the
    // canonical TouchpadVisualizer until they are folded into their surrounding
    // configuration writes. This method owns no visual state of its own.
    private void SyncGestureZoneOverlay()
    {
        Visualizer.Configuration = _configuration;
        RefreshGestureZoneVisuals(_signal);
    }
}
