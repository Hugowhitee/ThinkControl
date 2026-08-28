using System.Windows;
using ThinkControl.Core.Ipc;
using ThinkControl.Core.Notifications;
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
                status = await RefreshHardwareSetupStatusAsync().ConfigureAwait(true);
            }
            ShowFirstHardwareIssueOnce(status);
        }
        catch
        {
            State.DriverStatus = "Hardware status unavailable · open Inbox to retry";
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

            // Full provider refresh is reserved for the focused fan-provider retry.
            // It can rebuild PawnIO/LHM, X9 EC and keyboard together after returning
            // cooling to firmware. Sensor and keyboard prompts use narrower paths below.
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

    private string DescribeHardwareSetup(HardwareSetupStatus status)
    {
        if (!status.ServiceInstalled)
            return "ThinkControl hardware service not installed";
        if (!status.ServiceRunning)
            return "Hardware service stopped · action available in Inbox";
        if (!status.ServiceReachable)
            return "Hardware service running · app connection needs attention";
        if (status.LowLevelAccessRelevant && !status.LowLevelAccessInstalled)
        {
            return status.LowLevelAccessRegistered
                ? "PawnIO needs repair · action available in Inbox"
                : "PawnIO installation required · action available in Inbox";
        }
        if (status.LowLevelAccessRelevant && HasConcretePawnIoReadinessFailure(State.HardwareAccess))
            return "PawnIO needs repair · action available in Inbox";

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

    internal HardwarePrerequisiteIssue ResolvePrimaryHardwareIssue(HardwareSetupStatus status)
    {
        if (!status.ServiceRunning || !status.ServiceReachable)
            return HardwarePrerequisiteIssue.Service;
        if (status.LowLevelAccessRelevant &&
            (!status.LowLevelAccessInstalled || HasConcretePawnIoReadinessFailure(State.HardwareAccess)))
        {
            return HardwarePrerequisiteIssue.PawnIo;
        }
        if (!State.CanSensorTelemetry)
            return HardwarePrerequisiteIssue.Sensors;
        if (DeviceCapabilityExpectations.ExpectsWritableFanControl(State) && !State.CanFanControl)
            return HardwarePrerequisiteIssue.FanControl;
        if (DeviceCapabilityExpectations.ExpectsKeyboardBacklight(State) && !State.CanKeyboardBacklight)
            return HardwarePrerequisiteIssue.Keyboard;
        return HardwarePrerequisiteIssue.None;
    }

    private void ShowFirstHardwareIssueOnce(HardwareSetupStatus status)
    {
        HardwarePrerequisiteIssue issue = ResolvePrimaryHardwareIssue(status);
        if (issue == HardwarePrerequisiteIssue.None || !CanShowAttentionNow())
            return;

        string promptKey = $"{UpdateService.CurrentVersion}:{issue}";
        string[] prompted = UserSettings.Current.HardwareIssuePromptedKeys
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (prompted.Contains(promptKey, StringComparer.OrdinalIgnoreCase))
            return;

        string nextKeys = string.Join('|', prompted.Append(promptKey).TakeLast(12));
        UserSettings.Update(settings => settings with
        {
            HardwareIssuePromptedKeys = nextKeys,
            AttentionAcknowledgedKey = AttentionCooldownPolicy.HardwareKey(State.DriverStatus),
            AttentionAcknowledgedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        });
        ShowHardwareIssueWindow(issue);
    }

    public void OpenHardwareAttention()
    {
        _ = OpenCurrentHardwareIssueAsync();
    }

    public void OpenHardwareSetup()
    {
        _ = OpenCurrentHardwareIssueAsync();
    }

    internal void OpenHardwareIssue(HardwarePrerequisiteIssue issue) => ShowHardwareIssueWindow(issue);

    private async Task OpenCurrentHardwareIssueAsync()
    {
        HardwareSetupStatus status = await RefreshHardwareSetupStatusAsync().ConfigureAwait(true);
        ShowHardwareIssueWindow(ResolvePrimaryHardwareIssue(status));
    }

    internal void OpenSensorDetails(Window owner)
    {
        var window = new SensorDetailsWindow(this) { Owner = owner };
        window.Show();
        window.Activate();
    }

    private void ShowHardwareIssueWindow(HardwarePrerequisiteIssue issue)
    {
        if (_hardwareSetupWindow is { IsVisible: true })
        {
            _hardwareSetupWindow.Activate();
            return;
        }

        _hardwareSetupWindow = new HardwareSetupWindow(this, _hardwareSetupService, issue)
        {
            Owner = _advancedWindow?.IsVisible == true ? _advancedWindow : CompactWindow?.IsVisible == true ? CompactWindow : null
        };
        _hardwareSetupWindow.Closed += (_, _) => _hardwareSetupWindow = null;
        _hardwareSetupWindow.Show();
        _hardwareSetupWindow.Activate();
    }
}
