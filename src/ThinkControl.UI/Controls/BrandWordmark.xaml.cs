using System.Windows.Media;

namespace ThinkControl.UI.Controls;

public partial class BrandWordmark : System.Windows.Controls.UserControl
{
    public BrandWordmark()
    {
        InitializeComponent();

        // App-only optical correction requested for the WPF header: keep the
        // custom C fixed and move only "ontrol" eight canvas units to the right.
        // The canonical SVG/master geometry and CI-verifiable XAML transform stay
        // untouched, so packaging remains single-source and deterministic.
        ControlSuffix.RenderTransform = new TranslateTransform(-10, -0.5);
    }
}
