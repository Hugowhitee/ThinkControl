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
        bool serviceReady = status.ServiceRunning && status.ServiceReachable;

        ServiceStatusText.Text = status.ServiceDetail;
        ServiceStatusText.Foreground = (Brush)FindResource(serviceReady ? "Tc.Success" : "Tc.Warning");
        RepairServiceButton.Visibility = serviceReady ? Visibility.Collapsed : Visibility.Visible;

        LowLevelCard.Visibility = status.LowLevelAccessRelevant ? Visibility.Visible : Visibility.Collapsed;
        LowLevelStatusText.Text = status.LowLevelAccessDetail;
        LowLevelStatusText.Foreground = (Brush)FindResource(status.LowLevelAccessInstalled ? "Tc.Success" : "Tc.Warning");
        InstallLowLevelButton.Visibility = status.LowLevelAccessInstalled ? Visibility.Collapsed : Visibility.Visible;

        SensorProviderStatusText.Text = _app.State.CanSensorTelemetry
            ? $"Ready · {_app.State.SensorCountText}"
            : status.LowLevelAccessInstalled
                ? "PawnIO is installed. ThinkControl has not received useful LHM sensor telemetry yet; Recheck performs one clean provider rebuild."
                : "PawnIO is missing. Fix detected issues will install the verified package before rebuilding sensors.";
        SensorProviderStatusText.Foreground = (Brush)FindResource(_app.State.CanSensorTelemetry ? "Tc.Success" : "Tc.Warning");
        RetrySensorsButton.IsEnabled = serviceReady && status.LowLevelAccessInstalled;

        bool verifiedX9 = _app.State.MachineType is "21Q6" or "21Q7";
        FanProviderStatusText.Text = _app.State.CanFanControl
            ? $"Ready · verified X9 EC control · {_app.State.FanRpmText}"
            : _app.State.CanFanTelemetry
                ? "Fan telemetry is available, but the verified X9 EC control/readback gate has not passed. Recheck rebuilds the PawnIO EC transport once."
                : verifiedX9
                    ? "Verified X9 profile detected, but the EC read probe has not passed yet. ThinkControl keeps Lenovo firmware in control until the verified PawnIO/EC read gate succeeds."
                    : "Read-only fan telemetry can be discovered, but manual fan control is limited to verified device profiles.";
        FanProviderStatusText.Foreground = (Brush)FindResource(_app.State.CanFanControl ? "Tc.Success" : "Tc.Warning");
        RetryFanButton.IsEnabled = serviceReady && (!status.LowLevelAccessRelevant || status.LowLevelAccessInstalled);

        LenovoDriverStatusText.Text = _app.State.CanKeyboardBacklight
            ? $"Ready · {_app.State.KeyboardStatus}"
            : "Lenovo keyboard readback has not passed yet. Recheck probes PM/EnergyDrv/Vantage providers once; repeated failed probes are backed off automatically.";
        LenovoDriverStatusText.Foreground = (Brush)FindResource(_app.State.CanKeyboardBacklight ? "Tc.Success" : "Tc.Warning");
        RetryKeyboardButton.IsEnabled = serviceReady;

        if (string.IsNullOrWhiteSpace(ResultText.Text))
        {
            ResultText.Text = serviceReady
                ? "Use Fix detected issues for the recommended repair sequence. Individual Recheck buttons are only for targeted testing afterwards."
                : status.ServiceRunning
                    ? "The Windows service is running, but the ThinkControl app cannot reach its IPC endpoint. Fix detected issues will restart and re-register the service before hardware providers are touched."
                    : "Fix detected issues will repair the ThinkControl service first, then continue with hardware providers.";
        }
    }

    private async void FixDetectedIssues_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        SetBusy(true, "Checking service, PawnIO and hardware providers…");
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

            if (status.LowLevelAccessRelevant && !status.LowLevelAccessInstalled)
            {
                ResultText.Text = "PawnIO is required for the detected hardware. Downloading and SHA-256 verifying the signed installer…";
                HardwareSetupResult pawnIo = await _service.InstallLowLevelAccessAsync();
                if (!pawnIo.Success)
                {
                    ResultText.Text = pawnIo.Message;
                    await RefreshAsync();
                    return;
                }

                if (pawnIo.RestartRequired)
                {
                    ResultText.Text = "PawnIO installed successfully, but Windows requested a restart. Restart Windows once, then reopen Hardware setup; no repeated reinstall is needed.";
                    await RefreshAsync();
                    return;
                }
            }

            ResultText.Text = "Rebuilding LibreHardwareMonitor/PawnIO, X9 EC and Lenovo keyboard providers once…";
            await _app.RefreshHardwareProvidersAsync();
            await RefreshAsync();

            bool sensors = _app.State.CanSensorTelemetry;
            bool fanTelemetry = _app.State.CanFanTelemetry;
            bool fanControl = _app.State.CanFanControl;
            bool keyboard = _app.State.CanKeyboardBacklight;
            bool verifiedX9 = _app.State.MachineType is "21Q6" or "21Q7";

            var parts = new List<string>
            {
                sensors ? "Sensors ready" : "Sensors still unavailable",
                fanControl ? "X9 fan control ready" : fanTelemetry ? "Fan telemetry ready; manual control not verified" : "Fan provider still unavailable",
                keyboard ? "Keyboard ready" : "Keyboard provider still unavailable"
            };

            string next = string.Join(" · ", parts) + ".";
            if (!sensors || (verifiedX9 && !fanControl) || !keyboard)
            {
                next += " ThinkControl will not keep hammering failed providers in the background. " +
                        "If another trusted utility still sees hardware that ThinkControl does not, review the device report after this repair so it captures the exact post-repair provider state.";
            }
            else
            {
                next += " All detected hardware providers passed readback.";
            }

            ResultText.Text = next;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void RepairService_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;
        SetBusy(true, "Repairing the ThinkControl hardware service…");
        HardwareSetupResult result = await _service.RepairServiceAsync();
        ResultText.Text = result.Message;
        await _app.RefreshStatusAsync(forceSystemInfo: true);
        await RefreshAsync();
        SetBusy(false);
    }

    private async void InstallLowLevel_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;
        SetBusy(true, "Downloading and SHA-256 verifying PawnIO…");
        HardwareSetupResult result = await _service.InstallLowLevelAccessAsync();
        ResultText.Text = result.Message;
        if (result.Success && !result.RestartRequired)
            await _app.RefreshHardwareProvidersAsync();
        else
            await _app.RefreshStatusAsync(forceSystemInfo: true);
        await RefreshAsync();
        SetBusy(false);
    }

    private void OpenVantage_Click(object sender, RoutedEventArgs e)
    {
        if (LenovoSoftwareLauncher.TryOpenVantage())
        {
            ResultText.Text = "Lenovo Vantage opened. Use it only to install Lenovo platform/hotkey components if ThinkControl's clean keyboard readback still fails.";
            return;
        }

        ResultText.Text = "Lenovo Vantage is not registered on this Windows installation. ThinkControl does not require Vantage for PawnIO sensors or X9 EC fan access.";
    }

    private async void RetryProviders_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        string capability = sender is FrameworkElement { Tag: string tag } ? tag : "hardware";
        SetBusy(true, $"Rechecking {capability.ToLowerInvariant()} provider…");
        bool anyProvider = await _app.RefreshHardwareProvidersAsync();
        await RefreshAsync();

        bool ready = capability switch
        {
            "Sensors" => _app.State.CanSensorTelemetry,
            "Fans" => _app.State.CanFanControl || _app.State.CanFanTelemetry,
            "Keyboard" => _app.State.CanKeyboardBacklight,
            _ => anyProvider
        };

        ResultText.Text = ready
            ? $"{capability} provider responded after one clean refresh."
            : capability switch
            {
                "Sensors" => "Sensor provider is still empty after a clean LHM/PawnIO rebuild. ThinkControl will back off instead of continuously retrying.",
                "Fans" => "Fan provider is still unavailable after a clean PawnIO/EC rebuild. Lenovo firmware remains in control; no unknown EC writes were attempted.",
                "Keyboard" => "Keyboard provider still did not pass Lenovo readback. Check Lenovo platform/hotkey updates only after this clean recheck fails.",
                _ => "Provider refresh completed, but no hardware capability passed readback yet."
            };
        SetBusy(false);
    }

    private void Continue_Click(object sender, RoutedEventArgs e) => Close();

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        FixDetectedIssuesButton.IsEnabled = !busy;
        RepairServiceButton.IsEnabled = !busy;
        InstallLowLevelButton.IsEnabled = !busy;
        OpenVantageButton.IsEnabled = !busy;
        RetrySensorsButton.IsEnabled = !busy;
        RetryFanButton.IsEnabled = !busy;
        RetryKeyboardButton.IsEnabled = !busy;
        if (!string.IsNullOrWhiteSpace(message))
            ResultText.Text = message;
    }
}
