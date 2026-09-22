using System.Windows.Threading;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private static readonly TimeSpan AutomaticUpdateCheckStaleAfter = TimeSpan.FromHours(4);

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
        // Let the shell paint and hardware discovery begin first. Automatic update
        // discovery is event-driven rather than timer-driven: startup gets one stale
        // check, while later activation/resume events may request another only after
        // the persisted check timestamp is old enough.
        await Task.Delay(TimeSpan.FromSeconds(6)).ConfigureAwait(false);
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        await Dispatcher.InvokeAsync(
            RequestAutomaticUpdateCheckIfStale,
            DispatcherPriority.Background);
    }

    private void RequestAutomaticUpdateCheckIfStale()
    {
        if (_automaticUpdateBusy || !UserSettings.Current.AutomaticUpdates)
            return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset? last = UpdateCheckHistoryService.Read();
        if (last is DateTimeOffset checkedAt &&
            now - checkedAt >= TimeSpan.Zero &&
            now - checkedAt < AutomaticUpdateCheckStaleAfter)
        {
            return;
        }

        _ = CheckForUpdatesAutomaticallyAsync();
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
            // Record the attempt so repeated activations during an outage do not
            // hammer the release endpoint. A later stale activation/resume retries.
            UpdateCheckHistoryService.Record(DateTimeOffset.UtcNow);
            State.UpdateStatus = "Automatic update check failed safely";
        }
        finally
        {
            _automaticUpdateBusy = false;
        }
    }
}
