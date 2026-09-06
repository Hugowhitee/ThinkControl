using System.Drawing;
using System.Windows;
using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    private void OnShellIconStartup(object? sender, StartupEventArgs e)
    {
        // Application.Startup is raised from base.OnStartup before App.OnStartup
        // continues. Mark that one synchronous preflight as registry-only so shell,
        // tray and gesture startup never wait for the richer WMI inventory. The first
        // normal RefreshStatusAsync call immediately performs the full read on a worker.
        SystemStatusService.UseFastStartupReadOnce();
        ShowStartupBootstrapEarly();

        // The tray icon and Compact runtime are created later in App.OnStartup. Queue
        // both cosmetic icon replacement and configured touchpad input after that
        // handler yields. A silent --tray launch does not need a WPF activation event
        // before edge gestures become usable.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ApplyCanonicalTrayIcon));
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(StartConfiguredTouchpadInputForStartup));
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
