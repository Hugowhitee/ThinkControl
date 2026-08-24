using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _navigationPolishConfigured;

    private void ConfigureNavigationPolish()
    {
        if (_navigationPolishConfigured)
            return;
        _navigationPolishConfigured = true;

        // The old 980px minimum was wider than several pages actually need. Keep
        // enough room for the navigation rail + a 520px content surface, but let a
        // smaller Advanced window remain fully usable on compact displays.
        MinWidth = 780;
        MinHeight = 620;

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
    }

    private void AttachDynamicScrollReset(string navResource, string pageResource)
    {
        if (Resources[navResource] is RadioButton nav && Resources[pageResource] is ScrollViewer scroll)
            AttachScrollReset(nav, scroll);
    }

    private void AttachScrollReset(RadioButton nav, FrameworkElement page)
    {
        nav.Checked += (_, _) => Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (page is ScrollViewer direct)
                {
                    direct.ScrollToTop();
                    direct.ScrollToLeftEnd();
                    return;
                }

                if (FindFirstScrollViewer(page) is ScrollViewer nested)
                {
                    nested.ScrollToTop();
                    nested.ScrollToLeftEnd();
                }
            }));
    }

    private static ScrollViewer? FindFirstScrollViewer(DependencyObject parent)
    {
        if (parent is ScrollViewer scroll)
            return scroll;

        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            ScrollViewer? found = FindFirstScrollViewer(child);
            if (found is not null)
                return found;
        }
        return null;
    }
}