using System.Windows.Controls;
using System.Windows.Shapes;
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

        void Apply()
        {
            bool selected = nav.IsChecked == true;
            foreach (Path path in FindVisualChildren<Path>(nav))
                path.StrokeThickness = selected ? 1.8 : 1.35;

            foreach (PackIconLucide icon in FindVisualChildren<PackIconLucide>(nav))
            {
                icon.Width = selected ? 16.5 : 15;
                icon.Height = selected ? 16.5 : 15;
                icon.InvalidateVisual();
            }
        }

        nav.Checked += (_, _) => Apply();
        nav.Unchecked += (_, _) => Apply();
        Apply();
    }
}
