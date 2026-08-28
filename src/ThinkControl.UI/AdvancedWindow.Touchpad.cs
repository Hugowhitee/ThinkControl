using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string TouchpadPolishKey = "ThinkControl.Touchpad.PageConfiguration";

    private void ConfigureTouchpadPolish()
    {
        if (Resources.Contains(TouchpadPolishKey))
            return;

        Resources[TouchpadPolishKey] = true;

        // Keep click sensitivity in the same visual direction as every other slider.
        // The Windows value already maps naturally from low sensitivity / Firm to
        // high sensitivity / Light; an older RTL presentation inverted only the
        // accent fill and made the control look broken.
        if (TouchpadPanelControl.FindName("ClickForceSlider") is Slider clickForce)
        {
            clickForce.FlowDirection = FlowDirection.LeftToRight;
            clickForce.ToolTip = "Firm  ←  click sensitivity  →  Light";
        }

        TouchpadPanelControl.ConfigureValueFeedback();
    }
}
