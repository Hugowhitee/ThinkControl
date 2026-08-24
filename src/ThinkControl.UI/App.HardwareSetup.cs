using System.Windows;
using System.Windows.Threading;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private readonly HardwareSetupService _hardwareSetupService = new();
    private HardwareSetupWindow? _hardwareSetupWindow;
    private DispatcherTimer? _hardwareSetupTimer;
    private bool _hardwareSetupEvaluated;
    private bool _providerRefreshBusy;

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
            if (!status.NeedsAttention && State.CanSensorTelemetry && State.CanFanControl && State.CanKeyboardBacklight)
                return;

            string version = State.AppVersion ?? string.Empty;
            if (!string.Equals(UserSettings.Current.HardwareSetupPromptedVersion, version, StringComparison.OrdinalIgnoreCase))
                UserSettings.Update(settings => settings with { HardwareSetupPromptedVersion = version });
        }
        catch
        {
            State.DriverStatus = "Hardware status could not be refreshed";
        }
    }

    internal async Task<HardwareSetupStatus> RefreshHardwareSetupStatusAsync()
    {
        bool needsSensorProvider = !State.CanSensorTelemetry || !State.CanFanTelemetry || !State.CanFanControl;
        HardwareSetupStatus status = await _hardwareSetupService.ReadStatusAsync(State.MachineType, needsSensorProvider);
        State.DriverStatus = DescribeHardwareSetup(status);
        return status;
    }

    internal async Task<bool> RefreshHardwareProvidersAsync()
    {
        if (_providerRefreshBusy)
            return false;

        _providerRefreshBusy = true;
        try
        {
            State.DriverStatus = "Refreshing hardware providers…";
            ServiceResponse? response = await HardwareClient.RefreshProvidersAsync();
            if (response?.Success != true)
            {
                State.DriverStatus = response?.Error ?? "Hardware service did not accept provider refresh";
                return false;
            }

            // The service's background provider loop owns heavy LHM/EC discovery.
            // Give it one cycle, then read the fresh cached snapshot without blocking UI.
            await Task.Delay(2300);
            await RefreshStatusAsync(forceSystemInfo: false);
            await RefreshHardwareSetupStatusAsync();
            return State.CanSensorTelemetry || State.CanFanTelemetry || State.CanFanControl || State.CanKeyboardBacklight;
        }
        finally
        {
            _providerRefreshBusy = false;
        }
    }

    private string DescribeHardwareSetup(HardwareSetupStatus status)
    {
        if (!status.ServiceInstalled)
            return "ThinkControl hardware service not installed";
        if (!status.ServiceRunning)
            return "ThinkControl hardware service stopped · repair available";
        if (status.LowLevelAccessRelevant && !status.LowLevelAccessInstalled)
            return "PawnIO missing · install available";
        if (!State.CanSensorTelemetry || !State.CanFanControl || !State.CanKeyboardBacklight)
            return "Hardware service online · one or more providers need attention";
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
