using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI.Controls;

public partial class TouchpadPanel
{
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded += (_, _) => ApplyReleaseTouchpadPolish();
    }

    private void ApplyReleaseTouchpadPolish()
    {
        // Windows exposes clickForceSensitivity as a 0..100 sensitivity value.
        // Present it in the same direction as every other ThinkControl slider:
        // Firm (low sensitivity) -> Medium -> Light (high sensitivity). Reversing
        // only this track made its accent fill run from the opposite side.
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
