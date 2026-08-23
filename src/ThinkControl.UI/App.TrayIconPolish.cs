using System.Drawing;

namespace ThinkControl.UI;

public partial class App
{
    private Icon? _polishedTrayIcon;

    private void ApplyTrayIconPolish()
    {
        if (_trayIcon is null)
            return;

        try
        {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/ThinkControl.ico", UriKind.Absolute));
            if (resource?.Stream is null)
                return;

            using Icon source = new(resource.Stream);
            _polishedTrayIcon?.Dispose();
            _polishedTrayIcon = new Icon(source, new Size(32, 32));
            _trayIcon.Icon = _polishedTrayIcon;
        }
        catch
        {
            // The existing tray icon remains a safe fallback.
        }
    }
}
