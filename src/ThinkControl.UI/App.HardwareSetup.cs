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

            // Provider-unavailable states belong in Notifications and on their
            // respective pages. Only hard prerequisites and an explicitly diagnosed
            // PawnIO device/module failure get a one-time repair prompt. Opening the
            // prompt never starts UAC or an installer by itself.
            bool hardRequirementMissing =
                !status.ServiceInstalled ||
                !status.ServiceRunning ||
                !status.ServiceReachable ||
                (status.LowLevelAccessRelevant && !status.LowLevelAccessInstalled) ||
                HasConcretePawnIoReadinessFailure(State.HardwareAccess);

            if (!hardRequirementMissing)
                return;

            string version = State.AppVersion ?? string.Empty;
            if (string.Equals(UserSettings.Current.HardwareSetupPromptedVersion, version, StringComparison.OrdinalIgnoreCase))
                return;

            UserSettings.Update(settings => settings with { HardwareSetupPromptedVersion = version });
            Dispatcher.BeginInvoke(OpenHardwareSetup, DispatcherPriority.Background);
        }
        catch
        {
            State.DriverStatus = "Hardware status could not be refreshed";
        }
    }

    internal async Task<HardwareSetupStatus> RefreshHardwareSetupStatusAsync()
    {
        bool needsSensorProvider = !State.CanSensorTelemetry || !State.CanFanTelemetry || !State.CanFanControl;
        bool serviceReachable = await HardwareClient.PingAsync();
        HardwareSetupStatus status = await _hardwareSetupService.ReadStatusAsync(
            State.MachineType,
            needsSensorProvider,
            serviceReachable);
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
        if (!status.ServiceReachable)
            return "Hardware service is running · app connection needs repair";
        if (status.LowLevelAccessRelevant && !status.LowLevelAccessInstalled)
            return "PawnIO missing · install available";
        if (HasConcretePawnIoReadinessFailure(State.HardwareAccess))
            return "PawnIO installed · device/module repair available";
        if (!State.CanSensorTelemetry || !State.CanFanControl || !State.CanKeyboardBacklight)
            return "Hardware service online · one or more providers need attention";
        return "Ready";
    }

    private static bool HasConcretePawnIoReadinessFailure(string? detail)
    {
        string value = detail ?? string.Empty;
        return value.Contains("PawnIO is not installed", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("PawnIO is registered but its device is not available", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("access to its device was denied", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("LPC/ACPI EC module could not be loaded", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("PawnIO device could not be opened", StringComparison.OrdinalIgnoreCase);
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
