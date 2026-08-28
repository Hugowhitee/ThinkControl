using System.Windows.Threading;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private bool _automaticUpdateBusy;
    private bool _startupUpdateCheckScheduled;

    private void StartAutomaticUpdateChecks()
    {
        if (_startupUpdateCheckScheduled)
            return;

        _startupUpdateCheckScheduled = true;
        _ = CheckForUpdatesAfterStartupAsync();
    }

    private async Task CheckForUpdatesAfterStartupAsync()
    {
        // Let the shell paint and hardware discovery begin first. Unlike alpha.28,
        // this is intentionally a single startup check rather than a six-hour
        // polling timer; manual Check for updates remains available at any time.
        await Task.Delay(TimeSpan.FromSeconds(6)).ConfigureAwait(false);
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        await Dispatcher.InvokeAsync(
            () => _ = CheckForUpdatesAutomaticallyAsync(),
            DispatcherPriority.Background);
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
            PublishUpdateCheckResult(result);

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
            UpdateCheckHistoryService.Record(DateTimeOffset.UtcNow);
            State.UpdateStatus = "Startup update check failed safely";
        }
        finally
        {
            _automaticUpdateBusy = false;
        }
    }
}
