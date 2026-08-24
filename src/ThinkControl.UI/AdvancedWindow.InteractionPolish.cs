using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string InteractionPolishKey = "ThinkControl.Advanced.InteractionPolish";

    private void ConfigureInteractionPolish()
    {
        if (Resources.Contains(InteractionPolishKey))
            return;
        Resources[InteractionPolishKey] = true;

        AttachScrollReset(NavHome, PageHome);
        AttachScrollReset(NavPerformance, PagePerformance);
        AttachScrollReset(NavFans, PageFans);
        AttachScrollReset(NavDisplay, PageDisplay);
        AttachScrollReset(NavKeyboard, PageKeyboard);
        AttachScrollReset(NavBattery, PageBattery);
        AttachScrollReset(NavSystem, PageSystem);
        AttachScrollReset(NavUpdates, PageUpdates);
        AttachScrollReset(NavSettings, PageSettings);

        AttachDynamicScrollReset("ThinkControl.Dynamic.NavTouchpad", "ThinkControl.Dynamic.PageTouchpad");
        AttachDynamicScrollReset("ThinkControl.Dynamic.NavSensors", "ThinkControl.Dynamic.PageSensors");
        AttachDynamicScrollReset("ThinkControl.Dynamic.NavAudio", "ThinkControl.Dynamic.PageAudio");

        // The original dynamic Sensors icon captured the muted brush at creation
        // time. Bind it to its nav Foreground so selected/hover states match every
        // other icon. Audio and Touchpad already inherit Foreground directly.
        if (Resources["ThinkControl.Dynamic.NavSensors"] is RadioButton sensorsNav)
        {
            foreach (Path path in FindVisualChildren<Path>(sensorsNav))
            {
                path.SetBinding(Shape.StrokeProperty, new Binding(nameof(Foreground))
                {
                    Source = sensorsNav,
                    Mode = BindingMode.OneWay
                });
            }
        }

        // Give the 22 px switch its own vertical breathing room. Without this the
        // adaptive-brightness track can be clipped by the compact content row.
        FixSwitchRow(DisplayAdaptiveSwitch);
        FixSwitchRow(HomeAdaptiveSwitch);
    }

    private static void AttachScrollReset(RadioButton nav, ScrollViewer page)
    {
        nav.Checked += (_, _) => page.Dispatcher.BeginInvoke(() => page.ScrollToTop());
    }

    private void AttachDynamicScrollReset(string navKey, string pageKey)
    {
        if (Resources[navKey] is not RadioButton nav || Resources[pageKey] is not ScrollViewer page)
            return;
        AttachScrollReset(nav, page);
    }

    private static void FixSwitchRow(CheckBox toggle)
    {
        toggle.VerticalAlignment = VerticalAlignment.Center;
        toggle.Margin = new Thickness(0, 4, 0, 4);
        if (toggle.Parent is Grid row)
        {
            row.MinHeight = Math.Max(row.MinHeight, 32);
            row.ClipToBounds = false;
        }
    }
}
