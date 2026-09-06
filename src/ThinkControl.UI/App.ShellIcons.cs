using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private void OnShellIconStartup(object? sender, StartupEventArgs e)
    {
        // Application.Startup is raised from base.OnStartup before App.OnStartup
        // continues. Keep this hook intentionally tiny and registry-only: shell,
        // tray and gesture readiness must not wait for the richer WMI inventory.
        SystemStatusService.UseFastStartupReadOnce();
        ShowStartupBootstrapEarly();

        // Gesture input is user-session infrastructure, closer to a hotkey service
        // than to a settings page. Prime the cheap machine identity now so an X9 gets
        // the correct physical fallback geometry, then start configured Raw Input at
        // the earliest startup hook. On --tray launches EnsureInputStarted performs
        // the registration immediately; visible launches still defer the HID probe to
        // ContextIdle so first paint wins.
        try
        {
            StartupSystemIdentity identity = SystemStatusService.ReadStartupIdentity();
            if (!string.IsNullOrWhiteSpace(identity.DeviceName))
                State.DeviceName = identity.DeviceName;
            if (!string.IsNullOrWhiteSpace(identity.MachineType))
                State.MachineType = identity.MachineType;
            if (!string.IsNullOrWhiteSpace(identity.Manufacturer))
                _manufacturer = identity.Manufacturer;
            StartConfiguredTouchpadInputForStartup();
        }
        catch
        {
            // Gesture startup has its own activation recovery path. A malformed or
            // unavailable firmware identity must never prevent ThinkControl itself
            // from reaching the tray/UI.
        }

        // Cosmetic shell work stays deferred. The important difference is that edge
        // gestures no longer sit behind Compact-window construction, diagnostics or
        // service/hardware discovery during a silent Windows login.
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
