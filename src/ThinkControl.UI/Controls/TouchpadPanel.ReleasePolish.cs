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
        // Windows exposes clickForceSensitivity (higher = easier/lighter click).
        // Keep that native value untouched but present the physical force scale the
        // way people expect: Light on the left, Firm on the right.
        ClickForceSlider.IsDirectionReversed = true;

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
