using System.Windows.Threading;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private DispatcherTimer? _automaticUpdateTimer;
    private bool _automaticUpdateBusy;

    private void StartAutomaticUpdateChecks()
    {
        if (_automaticUpdateTimer is not null)
            return;

        _automaticUpdateTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromHours(6)
        };
        _automaticUpdateTimer.Tick += async (_, _) => await CheckForUpdatesAutomaticallyAsync();
        _automaticUpdateTimer.Start();

        // Startup itself should stay quick. The first check happens after the UI,
        // tray icon and hardware onboarding have had a chance to settle.
        var initial = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(8)
        };
        initial.Tick += async (_, _) =>
        {
            initial.Stop();
            await CheckForUpdatesAutomaticallyAsync();
        };
        initial.Start();
    }

    private async Task CheckForUpdatesAutomaticallyAsync()
    {
        if (_automaticUpdateBusy)
            return;

        _automaticUpdateBusy = true;
        try
        {
            UpdateCheckResult result = await UpdateService.CheckAsync();
            State.UpdateStatus = result.Status;

            if (_trayIcon is not null)
            {
                string text = result.Available && !string.IsNullOrWhiteSpace(result.Version)
                    ? $"ThinkControl · {result.Version} available"
                    : "ThinkControl";
                _trayIcon.Text = text.Length <= 63 ? text : text[..63];
            }
        }
        catch
        {
            // Update checks are informational and must never affect normal use.
        }
        finally
        {
            _automaticUpdateBusy = false;
        }
    }
}
