using System.Windows;

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
        // enough room for the navigation rail + a usable content surface while the
        // interaction layer remains the single owner of page scroll-reset behavior.
        MinWidth = 780;
        MinHeight = 620;
    }
}
