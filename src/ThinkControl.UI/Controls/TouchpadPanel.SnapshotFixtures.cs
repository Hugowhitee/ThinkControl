using System.Windows;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Controls;

public partial class TouchpadPanel
{
    /// <summary>
    /// Snapshot rendering has no physical Precision Touchpad, so haptic controls
    /// cannot inherit live Windows/provider state. Keep the deterministic fixture
    /// internally coherent with the synthetic detected-touchpad header without
    /// touching hardware or user settings.
    /// </summary>
    internal void PrepareHapticsForSnapshot()
    {
        HapticSwitch.IsEnabled = true;
        HapticSwitch.IsChecked = true;
        HapticStrengthSlider.IsEnabled = true;
        HapticStrengthSlider.Value = 50;
        HapticStrengthValue.Text = "Medium";
        ClickForceSlider.IsEnabled = true;
        ClickForceSlider.Value = 50;
        ClickForceValue.Text = "Medium";
        HapticStatusText.Text = "Uses the same discrete levels as Windows touchpad settings.";
    }

    /// <summary>
    /// Live corner Candidate/Active emphasis must never change editor visibility or
    /// child structure. Alpha.33 fixed the reflow regression; keep that invariant as
    /// an executable visual-QA assertion while still allowing opacity/hit-test state
    /// to communicate temporary runtime ownership.
    /// </summary>
    internal void ValidateCornerEditorLayoutForSnapshot(TouchpadCorner corner, bool live)
    {
        if (_selectedZone.Corner != corner)
            throw new InvalidOperationException("Snapshot corner selection drifted before layout validation.");
        if (_edgeEditorCard?.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("Selecting a corner must hide only the edge editor before live input begins.");
        if (_cornerEditorCard?.Visibility != Visibility.Visible)
            throw new InvalidOperationException("Selected corner editor must stay visible in both selected and live fixtures.");

        if (live)
        {
            if (_cornerEditorCard.IsHitTestVisible || _cornerEditorCard.Opacity >= 0.99)
                throw new InvalidOperationException("Live corner ownership must dim/disable the existing editor without reflowing it.");
        }
        else if (!_cornerEditorCard.IsHitTestVisible || Math.Abs(_cornerEditorCard.Opacity - 1) > 0.01)
        {
            throw new InvalidOperationException("Selected corner editor should be fully interactive outside live ownership.");
        }
    }
}
