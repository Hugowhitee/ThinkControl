using System.Windows;
using System.Windows.Controls;

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
                      option.Action == ThinkControl.Core.Touchpad.GestureActionKind.PreviousNextTrack;
        TrackCenterRow.Visibility = tracks ? Visibility.Visible : Visibility.Collapsed;
        TrackCenterPlayPauseSwitch.IsChecked = _configuration.TrackCenterPlayPauseEnabled;

        // TouchpadGestureZoneOverlay owns only the bounded, non-selectable center
        // target. Edge/corner selection and hover/live grammar stay in Visualizer.
        SyncGestureZoneOverlay();
    }

    private void TrackCenterPlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _host is null)
            return;

        _configuration = (_configuration with
        {
            TrackCenterPlayPauseEnabled = TrackCenterPlayPauseSwitch.IsChecked == true
        }).Sanitize();
        _host.UpdateConfiguration(_configuration);
        Visualizer.Configuration = _configuration;
        SyncTrackCenterOption();
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
