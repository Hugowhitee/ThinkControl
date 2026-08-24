using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string TouchpadPolishKey = "ThinkControl.Touchpad.Polish";

    private void ConfigureTouchpadPolish()
    {
        const string touchpadPageKey = "ThinkControl.Dynamic.PageTouchpad";
        if (Resources.Contains(TouchpadPolishKey) ||
            !Resources.Contains(touchpadPageKey) ||
            Resources[touchpadPageKey] is not ScrollViewer { Content: TouchpadPanel panel })
        {
            return;
        }

        Resources[TouchpadPolishKey] = true;

        // Windows reports click-force sensitivity numerically in the opposite
        // direction users describe it. Keep the native values untouched but render
        // the control intuitively: Light on the left, Medium in the middle, Firm on
        // the right.
        if (panel.FindName("ClickForceSlider") is Slider clickForce)
        {
            clickForce.FlowDirection = System.Windows.FlowDirection.RightToLeft;
            clickForce.ToolTip = "Light  ←  click sensitivity  →  Firm";
        }

        panel.ConfigureValueFeedback();
    }
}
