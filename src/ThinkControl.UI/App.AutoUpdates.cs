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
        if (_automaticUpdateBusy || !UserSettings.Current.AutomaticUpdates)
            return;

        _automaticUpdateBusy = true;
        State.UpdateStatus = "Checking for updates…";
        try
        {
            UpdateCheckResult result = await UpdateService.CheckAsync();
            UpdateCheckHistoryService.Record(DateTimeOffset.UtcNow);
            State.UpdateStatus = result.Status;

            if (_trayIcon is not null)
            {
                string text = result.Available && !string.IsNullOrWhiteSpace(result.Version)
                    ? $"ThinkControl · {result.Version} available"
                    : "ThinkControl";
                _trayIcon.Text = text.Length <= 63 ? text : text[..63];
            }

            if (!result.Available || string.IsNullOrWhiteSpace(result.InstallerUrl) || string.IsNullOrWhiteSpace(result.ChecksumUrl))
                return;

            State.UpdateStatus = $"Downloading {result.Version ?? "update"}…";
            UpdateInstallResult install = await UpdateService.DownloadAndLaunchAsync(result);
            State.UpdateStatus = install.Status;
            if (!install.Success)
                return;

            // The verified installer now owns the update transaction. Closing the
            // current process releases WPF files before setup replaces the payload;
            // /RELAUNCH=1 starts the new version when setup has completed.
            Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(ExitApplication));
        }
        catch
        {
            UpdateCheckHistoryService.Record(DateTimeOffset.UtcNow);
            State.UpdateStatus = "Automatic update check failed safely";
        }
        finally
        {
            _automaticUpdateBusy = false;
        }
    }
}
