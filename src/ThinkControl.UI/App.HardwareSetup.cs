using System.Windows;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private readonly HardwareSetupService _hardwareSetupService = new();
    private bool _hardwareSetupEvaluated;
    private HardwareSetupWindow? _hardwareSetupWindow;
    private bool _providerRefreshBusy;
    private bool _sensorRefreshBusy;
    private bool _keyboardRefreshBusy;
    private bool _hardwareRepairBusy;

    private void OnHardwareSetupActivated(object? sender, EventArgs e)
    {
        if (_hardwareSetupEvaluated)
            return;
        _hardwareSetupEvaluated = true;
        _ = EvaluateHardwareSetupSilentlyAsync();
    }

    private async Task EvaluateHardwareSetupSilentlyAsync()
    {
        try
        {
            await Task.Delay(900).ConfigureAwait(true);
            HardwareSetupStatus status = await RefreshHardwareSetupStatusAsync().ConfigureAwait(true);
            if (!status.ServiceReachable || State.DriverStatus != "Ready")
            {
                // Service/provider startup can trail the desktop after a reboot.
                // Give the existing installation one bounded warm-up retry before
                // presenting repair/setup as though the user must initialize again.
                await Task.Delay(3500).ConfigureAwait(true);
                await RefreshStatusAsync(forceSystemInfo: false).ConfigureAwait(true);
                await RefreshHardwareSetupStatusAsync().ConfigureAwait(true);
            }
        }
        catch
        {
            State.DriverStatus = "Hardware status unavailable · open Notifications to retry";
        }
    }

    internal async Task<HardwareSetupStatus> RefreshHardwareSetupStatusAsync()
    {
        bool expectsFanTelemetry = DeviceCapabilityExpectations.ExpectsFanTelemetry(State);
        bool expectsFanControl = DeviceCapabilityExpectations.ExpectsWritableFanControl(State);

        bool needsSensorProvider =
            !State.CanSensorTelemetry ||
            (expectsFanTelemetry && !State.CanFanTelemetry) ||
            (expectsFanControl && !State.CanFanControl);

        bool serviceReachable = await HardwareClient.PingAsync().ConfigureAwait(true);
        HardwareSetupStatus status = await _hardwareSetupService.ReadStatusAsync(
            State.MachineType,
            needsSensorProvider,
            serviceReachable).ConfigureAwait(true);
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
            ServiceResponse? response = await HardwareClient.RefreshProvidersAsync().ConfigureAwait(true);
            if (response?.Success != true)
            {
                State.DriverStatus = response?.Error ?? "Hardware service did not accept provider refresh";
                return false;
            }

            // Full provider repair is intentionally reserved for Hardware Setup.
            // It can rebuild PawnIO/LHM, X9 EC and keyboard together after returning
            // cooling to firmware. Page-level Retry actions use narrower paths below.
            await Task.Delay(2300).ConfigureAwait(true);
            await RefreshStatusAsync(forceSystemInfo: false).ConfigureAwait(true);
            await RefreshHardwareSetupStatusAsync().ConfigureAwait(true);
            return State.CanSensorTelemetry || State.CanFanTelemetry || State.CanFanControl || State.CanKeyboardBacklight;
        }
        finally
        {
            _providerRefreshBusy = false;
        }
    }

    internal async Task<bool> RefreshSensorProvidersAsync()
    {
        if (_sensorRefreshBusy)
            return false;

        _sensorRefreshBusy = true;
        try
        {
            State.DriverStatus = "Refreshing sensor provider…";
            ServiceResponse? response = await HardwareClient.RefreshSensorProvidersAsync().ConfigureAwait(true);
            if (response?.Success != true)
            {
                State.DriverStatus = response?.Error ?? "Sensor provider refresh was not accepted";
                return false;
            }

            await RefreshHardwareSetupStatusAsync().ConfigureAwait(true);
            return response.Capabilities?.SensorTelemetry == true;
        }
        finally
        {
            _sensorRefreshBusy = false;
        }
    }

    internal async Task<bool> RefreshKeyboardProviderAsync()
    {
        if (_keyboardRefreshBusy)
            return false;

        _keyboardRefreshBusy = true;
        try
        {
            State.DriverStatus = "Retrying Lenovo keyboard provider…";
            ServiceResponse? response = await HardwareClient.RefreshKeyboardProviderAsync().ConfigureAwait(true);
            if (response?.Success != true)
            {
                State.DriverStatus = response?.Error ?? "Keyboard provider refresh was not accepted";
                return false;
            }

            await RefreshHardwareSetupStatusAsync().ConfigureAwait(true);
            return response.Capabilities?.KeyboardBacklight == true;
        }
        finally
        {
            _keyboardRefreshBusy = false;
        }
    }

    internal async Task<HardwareSetupResult> RepairDetectedHardwareAsync()
    {
        if (_hardwareRepairBusy)
            return new(false, false, "A hardware repair is already running.");

        _hardwareRepairBusy = true;
        try
        {
            HardwareSetupStatus status = await RefreshHardwareSetupStatusAsync().ConfigureAwait(true);

            if (!status.ServiceRunning || !status.ServiceReachable)
            {
                State.DriverStatus = status.ServiceRunning
                    ? "Restarting hardware service…"
                    : "Starting hardware service…";
                HardwareSetupResult service = await _hardwareSetupService.RepairServiceAsync().ConfigureAwait(true);
                if (!service.Success)
                {
                    State.DriverStatus = service.Message;
                    return service;
                }

                await Task.Delay(650).ConfigureAwait(true);
                await RefreshStatusAsync(forceSystemInfo: true).ConfigureAwait(true);
                status = await RefreshHardwareSetupStatusAsync().ConfigureAwait(true);
                if (!status.ServiceReachable)
                {
                    const string connectionMessage = "The hardware service is running, but its local app connection did not become responsive. ThinkControl left the installation untouched. Retry once; if it still fails, review the hardware-service log from Diagnostics instead of reinstalling blindly.";
                    State.DriverStatus = connectionMessage;
                    return new(false, false, connectionMessage);
                }
            }

            bool pawnIoRepair = status.LowLevelAccessRelevant &&
                                (!status.LowLevelAccessInstalled || HasConcretePawnIoReadinessFailure(State.HardwareAccess));
            if (pawnIoRepair)
            {
                State.DriverStatus = status.LowLevelAccessInstalled
                    ? "Repairing low-level hardware access…"
                    : "Installing low-level hardware access…";
                HardwareSetupResult pawnIo = await _hardwareSetupService.InstallLowLevelAccessAsync().ConfigureAwait(true);
                if (!pawnIo.Success || pawnIo.RestartRequired)
                {
                    State.DriverStatus = pawnIo.Message;
                    return pawnIo;
                }
            }

            State.DriverStatus = "Verifying hardware providers…";
            await RefreshHardwareProvidersAsync().ConfigureAwait(true);
            HardwareSetupStatus finalStatus = await RefreshHardwareSetupStatusAsync().ConfigureAwait(true);

            bool expectedFanTelemetry = DeviceCapabilityExpectations.ExpectsFanTelemetry(State);
            bool expectedFanControl = DeviceCapabilityExpectations.ExpectsWritableFanControl(State);
            bool expectedKeyboard = DeviceCapabilityExpectations.ExpectsKeyboardBacklight(State);
            bool ready = finalStatus.ServiceRunning && finalStatus.ServiceReachable &&
                         (!finalStatus.LowLevelAccessRelevant || finalStatus.LowLevelAccessInstalled) &&
                         State.CanSensorTelemetry &&
                         (!expectedFanTelemetry || State.CanFanTelemetry) &&
                         (!expectedFanControl || State.CanFanControl) &&
                         (!expectedKeyboard || State.CanKeyboardBacklight);

            string message = ready
                ? "Hardware setup complete. All expected providers passed readback."
                : "Repair completed, but one or more hardware providers still did not pass readback. ThinkControl left unsupported writes disabled; check the provider details below.";
            State.DriverStatus = ready ? "Ready" : message;
            return new(ready, false, message);
        }
        finally
        {
            _hardwareRepairBusy = false;
        }
    }

    private string DescribeHardwareSetup(HardwareSetupStatus status)
    {
        if (!status.ServiceInstalled)
            return "ThinkControl hardware service not installed";
        if (!status.ServiceRunning)
            return "Hardware service stopped · action available in Notifications";
        if (!status.ServiceReachable)
            return "Hardware service running · app connection needs attention";
        if (status.LowLevelAccessRelevant && !status.LowLevelAccessInstalled)
            return "Low-level hardware access missing · action available in Notifications";
        if (status.LowLevelAccessRelevant && HasConcretePawnIoReadinessFailure(State.HardwareAccess))
            return "Low-level hardware access needs repair";

        bool providerAttention =
            !State.CanSensorTelemetry ||
            (DeviceCapabilityExpectations.ExpectsFanTelemetry(State) && !State.CanFanTelemetry) ||
            (DeviceCapabilityExpectations.ExpectsKeyboardBacklight(State) && !State.CanKeyboardBacklight) ||
            (DeviceCapabilityExpectations.ExpectsWritableFanControl(State) && !State.CanFanControl);
        return providerAttention
            ? "Hardware service online · one or more expected providers need attention"
            : "Ready";
    }

    private static bool HasConcretePawnIoReadinessFailure(string? detail)
    {
        string value = detail ?? string.Empty;
        return value.Contains("PawnIO is not installed", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("too old for", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("PawnIO is registered but its device is not available", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("access to its device was denied", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("LPC/ACPI EC module could not be loaded", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("PawnIO device could not be opened", StringComparison.OrdinalIgnoreCase);
    }

    public void OpenHardwareAttention()
    {
        OpenAdvanced("System");
        Dispatcher.BeginInvoke(ShowHardwareSetupWindow);
    }

    public void OpenHardwareSetup()
    {
        OpenAdvanced("System");
        Dispatcher.BeginInvoke(ShowHardwareSetupWindow);
    }

    internal void OpenSensorDetails(Window owner)
    {
        var window = new SensorDetailsWindow(this) { Owner = owner };
        window.Show();
        window.Activate();
    }

    private void ShowHardwareSetupWindow()
    {
        if (_hardwareSetupWindow is { IsVisible: true })
        {
            _hardwareSetupWindow.Activate();
            return;
        }

        _hardwareSetupWindow = new HardwareSetupWindow(this, _hardwareSetupService)
        {
            Owner = _advancedWindow
        };
        _hardwareSetupWindow.Closed += (_, _) => _hardwareSetupWindow = null;
        _hardwareSetupWindow.Show();
        _hardwareSetupWindow.Activate();
    }
}
