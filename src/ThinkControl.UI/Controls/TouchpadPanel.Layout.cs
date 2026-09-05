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

        if (tracks)
        {
            ActionHelpText.Text =
                "Use the left and right lane segments for Previous / Next. Tap the wider center segment for Play / Pause; all three actions share one continuous edge lane.";
        }

        // Edge/corner rendering plus the integrated Track center segment share the
        // canonical TouchpadVisualizer. There is no auxiliary center option/overlay.
        Visualizer.Configuration = _configuration;
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
