using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private UpdateCheckResult? _latestUpdateResult;

    internal UpdateCheckResult? LatestUpdateResult => _latestUpdateResult;
    internal event EventHandler? UpdateAvailabilityChanged;

    internal void PublishUpdateCheckResult(UpdateCheckResult result)
    {
        _latestUpdateResult = result;
        State.UpdateStatus = result.Status;
        UpdateAvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }
}