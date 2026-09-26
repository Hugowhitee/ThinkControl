using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private ThinkControlModeCoordinator? _modes;

    internal ThinkControlModeCoordinator Modes => _modes ??= new ThinkControlModeCoordinator(this);

    private async Task RestoreModeForExitAsync()
    {
        if (_modes is null || _modes.ActiveModeId == ThinkControlModeCatalog.NormalId)
            return;

        try { await _modes.ActivateAsync(ThinkControlModeCatalog.NormalId); }
        catch { }
    }
}
