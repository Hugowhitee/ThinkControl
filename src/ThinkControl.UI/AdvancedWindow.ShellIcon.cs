using System.Windows.Media.Imaging;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        try
        {
            // Set the Window icon before the first shell/taskbar presentation rather
            // than waiting for OnContentRendered. This avoids the blank taskbar icon
            // Windows can cache when the HWND is first created without one.
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/ThinkControl.ico", UriKind.Absolute));
        }
        catch
        {
            // The executable ApplicationIcon remains a fallback.
        }
    }
}
