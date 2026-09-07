using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Controls;

public partial class TouchpadPanel
{
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        ActionCombo.SelectionChanged += (_, _) => SyncTrackCenterOption();
        Loaded += (_, _) =>
        {
            ConfigureCornerLaunchUi();
            ApplyTouchpadLayout();
            SyncTrackCenterOption();
            SyncCornerLaunchControls();
            ApplySelectedZoneEditor();
        };
    }

    private void ApplyTouchpadLayout()
    {
        // Windows exposes clickForceSensitivity as a 0..100 sensitivity value.
        // Present it in the same direction as every other ThinkControl slider:
        // Firm (low sensitivity) -> Medium -> Light (high sensitivity).
        ClickForceSlider.IsDirectionReversed = false;

        // Values and their optional inline reset glyph share one compact metadata
        // column. This keeps the number readable without stealing track width.
        EnsureValueColumnWidth(SensitivityValue, 88);
        EnsureValueColumnWidth(HapticStrengthValue, 82);
        EnsureValueColumnWidth(ClickForceValue, 82);
        EnsureValueColumnWidth(OsdOpacityValue, 72);
        EnsureValueColumnWidth(EdgeWidthValue, 92);
        EnsureValueColumnWidth(ActivationValue, 92);
        EnsureValueColumnWidth(ToleranceValue, 92);
    }

    private void SyncTrackCenterOption()
    {
        bool tracks = ActionCombo.SelectedItem is ActionOption option &&
                      option.Action == GestureActionKind.PreviousNextTrack;

        TrackPlayPauseRow.Visibility = tracks ? Visibility.Visible : Visibility.Collapsed;
        TrackPlayPauseSwitch.IsChecked = tracks && _configuration.TrackCenterPlayPauseEnabled;

        if (tracks)
        {
            ActionHelpText.Text = _configuration.TrackCenterPlayPauseEnabled
                ? "Swipe the lane for Previous / Next. Play / Pause uses the visible center segment: hold for about half a second, then release. Release confirms the action so a resting touch cannot start media accidentally."
                : "Swipe the lane for Previous / Next. Play / Pause is off, so the center segment is not an active or visible control.";
        }

        // Edge/corner rendering plus the optional integrated Track center segment
        // share the canonical TouchpadVisualizer. This toggle changes one Track
        // affordance; it does not create a separate edge action or overlay owner.
        Visualizer.Configuration = _configuration;
    }

    private void TrackPlayPauseSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _host is null || _selectedZone.Edge is null ||
            _configuration.BindingFor(SelectedEdge).Action != GestureActionKind.PreviousNextTrack)
        {
            return;
        }

        bool enabled = TrackPlayPauseSwitch.IsChecked == true;
        _configuration = (_configuration with
        {
            TrackCenterPlayPauseDisabled = !enabled
        }).Sanitize();

        _host.UpdateConfiguration(_configuration);
        Visualizer.Configuration = _configuration;
        SyncGestureZoneOverlay();
        SyncTrackCenterOption();
        GestureStatusText.Text = enabled
            ? "Track control · Previous / Play-Pause / Next."
            : "Track control · Previous / Next only.";
    }

    private static void EnsureValueColumnWidth(TextBlock value, double minimumWidth)
    {
        if (value.Parent is not Grid grid)
            return;

        int column = Grid.GetColumn(value);
        if (column < 0 || column >= grid.ColumnDefinitions.Count)
        {
            value.MinWidth = Math.Max(value.MinWidth, minimumWidth);
            return;
        }

        ColumnDefinition definition = grid.ColumnDefinitions[column];
        if (definition.Width.IsAbsolute && definition.Width.Value < minimumWidth)
            definition.Width = new GridLength(minimumWidth);

        value.MinWidth = Math.Max(value.MinWidth, minimumWidth - 30);
    }
}
