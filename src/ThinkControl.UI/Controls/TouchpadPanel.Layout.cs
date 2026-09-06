using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Controls;

public partial class TouchpadPanel
{
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        // Play/Pause is part of Track control now. Keep the legacy enum value only so
        // old numeric settings can migrate; do not offer a second menu action that
        // duplicates the center segment.
        ActionOption[] currentOptions = ActionCombo.Items
            .Cast<ActionOption>()
            .Where(option => option.Action != GestureActionKind.PlayPause)
            .ToArray();
        ActionCombo.ItemsSource = currentOptions;

        // Replace the original move-only assignment handler with swap semantics. If
        // the requested action already lives on another edge, the selected edge's
        // previous action moves there instead of leaving that edge Off.
        ActionCombo.SelectionChanged -= ActionCombo_SelectionChanged;
        ActionCombo.SelectionChanged += ActionCombo_SwapSelectionChanged;
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

    private void ActionCombo_SwapSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _host is null || _selectedZone.Edge is null ||
            ActionCombo.SelectedItem is not ActionOption option)
        {
            return;
        }

        ActionHelpText.Text = option.Description;
        TouchpadEdge selectedEdge = SelectedEdge;
        TouchpadEdgeBinding selectedBinding = _configuration.BindingFor(selectedEdge);
        if (selectedBinding.Action == option.Action)
            return;

        TouchpadGestureBindings bindings = _configuration.Bindings ?? TouchpadGestureBindings.AsusStyle;
        TouchpadEdge? occupiedEdge = null;
        TouchpadEdgeBinding? occupiedBinding = null;

        if (option.Action != GestureActionKind.Disabled)
        {
            foreach (TouchpadEdge edge in Enum.GetValues<TouchpadEdge>())
            {
                if (edge == selectedEdge)
                    continue;
                TouchpadEdgeBinding existing = bindings.Get(edge).Sanitize();
                if (existing.Action != option.Action)
                    continue;
                occupiedEdge = edge;
                occupiedBinding = existing;
                break;
            }
        }

        if (occupiedEdge is not TouchpadEdge previous || occupiedBinding is null)
        {
            SetSelectedBinding(selectedBinding with { Action = option.Action });
            return;
        }

        // Sensitivity/inversion belong to the physical edge, not to the action being
        // moved. Swap only the action kinds so each edge keeps its own tuning.
        bindings = WithBinding(
            bindings,
            previous,
            occupiedBinding with { Action = selectedBinding.Action });
        bindings = WithBinding(
            bindings,
            selectedEdge,
            selectedBinding with { Action = option.Action });

        _configuration = (_configuration with { Bindings = bindings }).Sanitize();
        _host.UpdateConfiguration(_configuration);
        Visualizer.Configuration = _configuration;
        SyncGestureZoneOverlay();
        SensitivityValue.Text = FormatSensitivity(selectedBinding.Sensitivity);

        GestureStatusText.Text = selectedBinding.Action == GestureActionKind.Disabled
            ? $"{ActionLabel(option.Action)} moved from {EdgeLabel(previous)} to {EdgeLabel(selectedEdge)}."
            : $"Swapped {ActionLabel(option.Action)} and {ActionLabel(selectedBinding.Action)} between {EdgeLabel(previous)} and {EdgeLabel(selectedEdge)}.";
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
                "Use the left and right lane segments for Previous / Next. Tap the center segment for Play / Pause; small finger drift is tolerated so a normal quick tap does not fall into a dead zone.";
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
