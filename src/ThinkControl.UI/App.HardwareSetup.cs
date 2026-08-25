using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private readonly HardwareSetupService _hardwareSetupService = new();
    private bool _hardwareSetupEvaluated;
    private bool _providerRefreshBusy;
    private bool _hardwareRepairBusy;

    private void OnHardwareSetupActivated(object? sender, EventArgs e)
    {
        if (_hardwareSetupEvaluated)
            return;
        _hardwareSetupEvaluated = true;

        // Hardware discovery is silent on startup. Missing components are surfaced
        // inside ThinkControl's notification/System UI; a repair window must never
        // cover the app or steal focus simply because a service is stopped.
        _ = EvaluateHardwareSetupSilentlyAsync();
    }

    private async Task EvaluateHardwareSetupSilentlyAsync()
    {
        try
        {
            await Task.Delay(900).ConfigureAwait(true);
            await RefreshHardwareSetupStatusAsync().ConfigureAwait(true);
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

            // Heavy provider discovery belongs to the service. The UI only waits for
            // one bounded service cycle and then consumes its cached status.
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
                    const string connectionMessage = "The ThinkControl hardware service is running, but the app connection still does not respond. Reinstall ThinkControl if this persists.";
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
        _advancedWindow?.ShowNotificationSheet();
    }

    public void OpenHardwareSetup()
    {
        // Kept as the public call site for older UI code, but setup is now an
        // in-app notification/System workflow rather than a separate Window.
        OpenHardwareAttention();
    }
}
