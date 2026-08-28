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
}
