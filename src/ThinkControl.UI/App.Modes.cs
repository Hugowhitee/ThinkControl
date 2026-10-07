using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private ThinkControlModeCoordinator? _modes;
    private ThinkControlModeAutomationService? _modeAutomation;

    internal ThinkControlModeCoordinator Modes => _modes ??= new ThinkControlModeCoordinator(this);

    internal ThinkControlModeAutomationService ModeAutomation =>
        _modeAutomation ??= new ThinkControlModeAutomationService(this);

    internal void InitializeModeAutomation()
    {
        Startup += (_, _) => { if (!IsVisualQa) ModeAutomation.Start(); };
        Exit += (_, _) =>
        {
            _modeAutomation?.Dispose();
            _modeAutomation = null;
        };
    }

    internal void RequestModeAutomationEvaluation() =>
        _modeAutomation?.RequestEvaluation();

    internal void NotifyManualModeSelection() =>
        _modeAutomation?.SuppressUntilContextChanges();

    internal void ShowAutomaticModeActivation(string ruleName)
    {
        if (!CanShowAttentionNow() || _attentionToast.HasActionableAttention ||
            !Modes.ActiveModeAutomatic || Modes.IsTransitioning || Modes.IsModified || Modes.SettingsNeedChecking)
            return;
        _attentionToast.ShowPassive(
            $"mode-active:{Modes.ActiveModeId}:{ModeAutomation.ActiveRuleId}",
            $"{Modes.ActiveModeName} enabled",
            $"By {ruleName}",
            TimeSpan.FromSeconds(5));
    }

    private async Task RestoreModeForExitAsync()
    {
        if (_modes is null || _modes.ActiveModeId == ThinkControlModeCatalog.NormalId)
            return;

        try
        {
            await _modes.ActivateAsync(
                ThinkControlModeCatalog.NormalId,
                ThinkControlModeActivationOrigin.Restore);
        }
        catch { }
    }
}
