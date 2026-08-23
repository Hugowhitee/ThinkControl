using System.Drawing;
using System.Windows;
using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    private void OnShellIconStartup(object? sender, StartupEventArgs e)
    {
        // Application.Startup is raised from base.OnStartup before the tray icon is
        // created by App.OnStartup. Queue this so it runs just after startup and
        // replaces the old tray-only artwork with the canonical app icon.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ApplyCanonicalTrayIcon));
    }

    private void ApplyCanonicalTrayIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/ThinkControl.ico", UriKind.Absolute));
            if (resource?.Stream is null)
                return;

            using Icon source = new(resource.Stream);
            Icon replacement = (Icon)source.Clone();
            Icon? previous = _ownedTrayIcon;
            _ownedTrayIcon = replacement;

            if (_trayIcon is not null)
                _trayIcon.Icon = replacement;

            previous?.Dispose();
        }
        catch
        {
            // Shell icon polish is cosmetic. CreateTrayIcon already has a safe
            // executable/fallback path, so startup must never fail here.
        }
    }
}
