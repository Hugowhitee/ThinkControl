using System.Windows;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class HardwareSetupWindow : Window
{
    private readonly App _app;
    private readonly HardwareSetupService _service;
    private bool _busy;

    internal HardwareSetupWindow(App app, HardwareSetupService service)
    {
        _app = app;
        _service = service;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        HardwareSetupStatus status = await _app.RefreshHardwareSetupStatusAsync();
        PresentStatus(status, _app.SystemStatusService.Read().Manufacturer.Contains("Lenovo", StringComparison.OrdinalIgnoreCase));
    }

    private void PresentStatus(HardwareSetupStatus status, bool lenovoDevice)
    {
        bool serviceReady = status.ServiceRunning && status.ServiceReachable;
        bool pawnIoRepair = IsPawnIoRepairRecommended(status);
        bool verifiedX9 = _app.State.MachineType is "21Q6" or "21Q7";
        SetupIntroText.Text = "ThinkControl checks only the controls this PC can use.";

        if (!serviceReady)
        {
            PrimaryTitleText.Text = "ThinkControl service needs repair";
            PrimaryStatusText.Text = "The hardware service is not responding, so hardware changes are safely unavailable.";
            ProgressStepsText.Text = "Repair service  →  reconnect  →  verify";
            FixDetectedIssuesButton.Content = "Repair service";
        }
        else if (status.LowLevelAccessRelevant && pawnIoRepair)
        {
            PrimaryTitleText.Text = "Hardware access needs repair";
            PrimaryStatusText.Text = "Sensors and fan control are waiting for the verified hardware component to be repaired.";
            ProgressStepsText.Text = "Download verified component  →  repair  →  verify controls";
            FixDetectedIssuesButton.Content = "Repair hardware access";
        }
        else if (!_app.State.CanSensorTelemetry)
        {
            PrimaryTitleText.Text = "Sensors need a recheck";
            PrimaryStatusText.Text = "ThinkControl has not received useful sensor readings yet. No driver installation is needed.";
            ProgressStepsText.Text = "Refresh provider  →  read sensors  →  verify";
            FixDetectedIssuesButton.Content = "Recheck sensors";
        }
        else if (verifiedX9 && !_app.State.CanFanControl)
        {
            PrimaryTitleText.Text = "Fan control needs a recheck";
            PrimaryStatusText.Text = "Firmware remains safely in control until ThinkControl verifies the supported fan path.";
            ProgressStepsText.Text = "Refresh provider  →  read back  →  verify";
            FixDetectedIssuesButton.Content = "Recheck fan control";
        }
        else if (lenovoDevice && !_app.State.CanKeyboardBacklight)
        {
            PrimaryTitleText.Text = "Keyboard controls need a recheck";
            PrimaryStatusText.Text = "ThinkControl has not verified the Lenovo keyboard provider yet.";
            ProgressStepsText.Text = "Refresh provider  →  read back  →  verify";
            FixDetectedIssuesButton.Content = "Recheck keyboard controls";
        }
        else
        {
            PrimaryTitleText.Text = "Everything is ready";
            PrimaryStatusText.Text = "All controls detected for this PC passed their safety checks.";
            ProgressStepsText.Text = "No repair is needed. You can close this window.";
            FixDetectedIssuesButton.Content = "Check again";
        }

        if (string.IsNullOrWhiteSpace(ResultText.Text))
            ResultText.Text = serviceReady
                ? "ThinkControl only asks Windows for permission when the selected repair needs it."
                : "Repairing the service does not change fan, keyboard or display settings.";
    }

    private async void FixDetectedIssues_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        SetBusy(true, "Checking what needs attention…");
        try
        {
            HardwareSetupStatus status = await _app.RefreshHardwareSetupStatusAsync();

            if (!status.ServiceRunning || !status.ServiceReachable)
            {
                ResultText.Text = status.ServiceRunning
                    ? "Restarting the ThinkControl hardware service to restore the app connection…"
                    : "Repairing the ThinkControl hardware service…";
                HardwareSetupResult serviceRepair = await _service.RepairServiceAsync();
                if (!serviceRepair.Success)
                {
                    ResultText.Text = serviceRepair.Message;
                    await RefreshAsync();
                    return;
                }

                await _app.RefreshStatusAsync(forceSystemInfo: true);
                status = await _app.RefreshHardwareSetupStatusAsync();
                if (!status.ServiceReachable)
                {
                    ResultText.Text = "The service is running after repair, but the app connection still does not respond. Reinstall ThinkControl if this persists; hardware writes remain disabled.";
                    await RefreshAsync();
                    return;
                }
            }

            bool pawnIoRepair = IsPawnIoRepairRecommended(status);
            if (status.LowLevelAccessRelevant && (!status.LowLevelAccessInstalled || pawnIoRepair))
            {
                ResultText.Text = status.LowLevelAccessInstalled
                    ? "Downloading the verified repair component…"
                    : "Downloading the verified hardware component…";
                ProgressStepsText.Text = "Download verified component  →  repair  →  verify controls";
                HardwareSetupResult pawnIo = await _service.InstallLowLevelAccessAsync();
                if (!pawnIo.Success)
                {
                    ResultText.Text = pawnIo.Message;
                    await RefreshAsync();
                    return;
                }

                if (pawnIo.RestartRequired)
                {
                    ResultText.Text = "PawnIO repair completed, but Windows requested a restart. Restart Windows once, then reopen Hardware setup; ThinkControl will not keep reinstalling it automatically.";
                    await RefreshAsync();
                    return;
                }
            }

            ResultText.Text = "Checking repaired controls…";
            ProgressStepsText.Text = "Refresh provider  →  read back  →  verify";
            await _app.RefreshHardwareProvidersAsync();
            await RefreshAsync();

            bool sensors = _app.State.CanSensorTelemetry;
            bool fanTelemetry = _app.State.CanFanTelemetry;
            bool fanControl = _app.State.CanFanControl;
            bool keyboard = _app.State.CanKeyboardBacklight;
            bool expectsFanTelemetry = DeviceCapabilityExpectations.ExpectsFanTelemetry(_app.State);
            bool expectsFanControl = DeviceCapabilityExpectations.ExpectsWritableFanControl(_app.State);
            bool expectsKeyboard = DeviceCapabilityExpectations.ExpectsKeyboardBacklight(_app.State);

            bool allReady = sensors &&
                            (!expectsFanTelemetry || fanTelemetry) &&
                            (!expectsFanControl || fanControl) &&
                            (!expectsKeyboard || keyboard);
            ResultText.Text = allReady
                ? "Repair complete. ThinkControl verified the controls expected for this PC."
                : "Check complete. The remaining unavailable control stays safely managed by Windows or firmware.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private bool IsPawnIoRepairRecommended(HardwareSetupStatus status)
    {
        if (!status.LowLevelAccessRelevant)
            return false;
        if (!status.LowLevelAccessInstalled)
            return true;

        string detail = _app.State.HardwareAccess ?? string.Empty;
        return detail.Contains("PawnIO is not installed", StringComparison.OrdinalIgnoreCase) ||
               detail.Contains("too old for", StringComparison.OrdinalIgnoreCase) ||
               detail.Contains("PawnIO is registered but its device is not available", StringComparison.OrdinalIgnoreCase) ||
               detail.Contains("access to its device was denied", StringComparison.OrdinalIgnoreCase) ||
               detail.Contains("LPC/ACPI EC module could not be loaded", StringComparison.OrdinalIgnoreCase) ||
               detail.Contains("PawnIO device could not be opened", StringComparison.OrdinalIgnoreCase);
    }

    private void Continue_Click(object sender, RoutedEventArgs e) => Close();

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        FixDetectedIssuesButton.IsEnabled = !busy;
        if (!string.IsNullOrWhiteSpace(message))
            ResultText.Text = message;
    }
}
