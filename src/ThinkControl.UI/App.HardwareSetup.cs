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
            HardwareSetupStatus status = await RefreshHardwareSetupStatusAsync();
            if (!status.NeedsAttention)
                return;

            // Do not interrupt startup with a modal setup window. Compact and
            // Advanced surface the same status with a red attention dot; the user
            // can open Hardware setup from there and repair/install in one flow.
            string version = State.AppVersion ?? string.Empty;
            if (!string.Equals(UserSettings.Current.HardwareSetupPromptedVersion, version, StringComparison.OrdinalIgnoreCase))
                UserSettings.Update(settings => settings with { HardwareSetupPromptedVersion = version });
        }
        catch
        {
            State.DriverStatus = "Hardware service status unavailable";
        }
    }

    internal async Task<HardwareSetupStatus> RefreshHardwareSetupStatusAsync()
    {
        bool needsSensorProvider = !State.CanSensorTelemetry || !State.CanFanTelemetry;
        HardwareSetupStatus status = await _hardwareSetupService.ReadStatusAsync(State.MachineType, needsSensorProvider);
        State.DriverStatus = DescribeHardwareSetup(status);
        return status;
    }

    private static string DescribeHardwareSetup(HardwareSetupStatus status)
    {
        if (!status.ServiceInstalled)
            return "ThinkControl hardware service not installed";
        if (!status.ServiceRunning)
            return "ThinkControl hardware service stopped · repair available";
        if (status.LowLevelAccessRelevant && !status.LowLevelAccessInstalled)
            return "Hardware provider recommended · setup available";
        return "Ready";
    }

    public void OpenHardwareAttention()
    {
        OpenAdvanced("System");
        Dispatcher.BeginInvoke(() => OpenHardwareSetup(), DispatcherPriority.Background);
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
