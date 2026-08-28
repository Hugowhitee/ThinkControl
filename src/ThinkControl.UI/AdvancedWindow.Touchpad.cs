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

        // Keep click sensitivity in the same visual direction as every other slider.
        // The Windows value already maps naturally from low sensitivity / Firm to
        // high sensitivity / Light; an older RTL presentation inverted only the
        // accent fill and made the control look broken.
        if (panel.FindName("ClickForceSlider") is Slider clickForce)
        {
            clickForce.FlowDirection = FlowDirection.LeftToRight;
            clickForce.ToolTip = "Firm  ←  click sensitivity  →  Light";
        }

        panel.ConfigureValueFeedback();
    }
}
