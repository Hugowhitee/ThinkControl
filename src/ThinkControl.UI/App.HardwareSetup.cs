using System.Windows;
using System.Windows.Threading;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private readonly HardwareSetupService _hardwareSetupService = new();
    private HardwareSetupWindow? _hardwareSetupWindow;
    private DispatcherTimer? _hardwareSetupTimer;
    private bool _hardwareSetupEvaluated;

    private void OnHardwareSetupActivated(object? sender, EventArgs e)
    {
        if (_hardwareSetupEvaluated || _hardwareSetupTimer is not null)
            return;

        _hardwareSetupTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _hardwareSetupTimer.Tick += HardwareSetupTimer_Tick;
        _hardwareSetupTimer.Start();
    }

    private async void HardwareSetupTimer_Tick(object? sender, EventArgs e)
    {
        _hardwareSetupTimer?.Stop();
        _hardwareSetupTimer = null;
        if (_hardwareSetupEvaluated)
            return;
        _hardwareSetupEvaluated = true;

        try
        {
            HardwareSetupStatus status = await _hardwareSetupService.ReadStatusAsync(State.MachineType);
            if (!status.NeedsAttention)
                return;

            string version = State.AppVersion ?? string.Empty;
            if (string.Equals(UserSettings.Current.HardwareSetupPromptedVersion, version, StringComparison.OrdinalIgnoreCase))
                return;

            UserSettings.Update(settings => settings with { HardwareSetupPromptedVersion = version });
            OpenHardwareSetup();
        }
        catch
        {
            // Hardware onboarding must never interfere with normal application startup.
        }
    }

    public void OpenHardwareSetup()
    {
        if (_hardwareSetupWindow is null)
        {
            _hardwareSetupWindow = new HardwareSetupWindow(this, _hardwareSetupService);
            _hardwareSetupWindow.Closed += (_, _) => _hardwareSetupWindow = null;
        }

        Window? owner = _advancedWindow?.IsVisible == true ? _advancedWindow : CompactWindow;
        if (owner?.IsVisible == true)
            _hardwareSetupWindow.Owner = owner;

        if (!_hardwareSetupWindow.IsVisible)
            _hardwareSetupWindow.Show();
        if (_hardwareSetupWindow.WindowState == WindowState.Minimized)
            _hardwareSetupWindow.WindowState = WindowState.Normal;
        _hardwareSetupWindow.Activate();
    }
}
