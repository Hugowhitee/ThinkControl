using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private void ConfigureDynamicNavIconWeight()
    {
        ConfigureDynamicNavIconWeight("ThinkControl.Dynamic.NavSensors");
        ConfigureDynamicNavIconWeight("ThinkControl.Dynamic.NavAudio");
    }

    private void ConfigureDynamicNavIconWeight(string resourceKey)
    {
        if (Resources[resourceKey] is not RadioButton nav)
            return;

        // TcNav deliberately makes the selected label SemiBold. These two dynamic
        // pages use stroked custom glyphs, so pin the icon itself to Normal and the
        // same 15×15 box as every static navigation icon. Selection should brighten,
        // never grow or change visual weight.
        foreach (PackIconLucide icon in FindVisualChildren<PackIconLucide>(nav))
        {
            icon.Width = 15;
            icon.Height = 15;
            icon.FontWeight = FontWeights.Normal;
            icon.InvalidateVisual();
        }
    }
}
