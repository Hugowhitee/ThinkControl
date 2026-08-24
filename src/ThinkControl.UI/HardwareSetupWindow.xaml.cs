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

        ServiceStatusText.Text = status.ServiceDetail;
        ServiceStatusText.Foreground = (Brush)FindResource(status.ServiceRunning ? "Tc.Success" : "Tc.Warning");
        RepairServiceButton.Visibility = status.ServiceRunning ? Visibility.Collapsed : Visibility.Visible;

        LowLevelCard.Visibility = status.LowLevelAccessRelevant ? Visibility.Visible : Visibility.Collapsed;
        LowLevelStatusText.Text = status.LowLevelAccessDetail;
        LowLevelStatusText.Foreground = (Brush)FindResource(status.LowLevelAccessInstalled ? "Tc.Success" : "Tc.Warning");
        InstallLowLevelButton.Visibility = status.LowLevelAccessInstalled ? Visibility.Collapsed : Visibility.Visible;

        SensorProviderStatusText.Text = _app.State.CanSensorTelemetry
            ? $"Ready · {_app.State.SensorCountText}"
            : status.LowLevelAccessInstalled
                ? "PawnIO is installed, but ThinkControl's LibreHardwareMonitor provider has not returned useful sensors yet. Retry rebuilds the provider."
                : "Waiting for PawnIO installation before LHM sensor discovery can be retried.";
        SensorProviderStatusText.Foreground = (Brush)FindResource(_app.State.CanSensorTelemetry ? "Tc.Success" : "Tc.Warning");
        RetrySensorsButton.IsEnabled = status.ServiceRunning && status.LowLevelAccessInstalled;

        bool verifiedX9 = _app.State.MachineType is "21Q6" or "21Q7";
        FanProviderStatusText.Text = _app.State.CanFanControl
            ? $"Ready · verified X9 EC control · {_app.State.FanRpmText}"
            : _app.State.CanFanTelemetry
                ? "Fan telemetry is available, but the verified X9 EC read/write probe has not passed yet. Retry rebuilds PawnIO/EC access without guessing registers."
                : verifiedX9
                    ? "Verified X9 profile detected, but EC/fan telemetry has not passed readback yet. Retry the provider before reinstalling anything."
                    : "Read-only fan telemetry can be discovered, but manual fan control is limited to verified device profiles.";
        FanProviderStatusText.Foreground = (Brush)FindResource(_app.State.CanFanControl ? "Tc.Success" : "Tc.Warning");
        RetryFanButton.IsEnabled = status.ServiceRunning && (!status.LowLevelAccessRelevant || status.LowLevelAccessInstalled);

        LenovoDriverStatusText.Text = _app.State.CanKeyboardBacklight
            ? $"Ready · {_app.State.KeyboardStatus}"
            : "Lenovo keyboard readback has not passed yet. Retry probes PM/EnergyDrv/Vantage providers again; this does not install or change a driver.";
        LenovoDriverStatusText.Foreground = (Brush)FindResource(_app.State.CanKeyboardBacklight ? "Tc.Success" : "Tc.Warning");
        RetryKeyboardButton.IsEnabled = status.ServiceRunning;

        if (string.IsNullOrWhiteSpace(ResultText.Text))
        {
            ResultText.Text = status.ServiceRunning
                ? "Choose Retry on the capability that is failing. ThinkControl will recycle its providers and show the new readback here."
                : "Repair the ThinkControl service first; provider diagnostics depend on that connection.";
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
            ResultText.Text = "Lenovo Vantage opened. Only install Lenovo platform/interface updates if Retry still cannot read the keyboard provider.";
            return;
        }

        ResultText.Text = "Lenovo Vantage is not registered on this Windows installation. Retry does not require it; install Vantage only if Lenovo platform components are genuinely missing.";
    }

    private async void RetryProviders_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        string capability = sender is FrameworkElement { Tag: string tag } ? tag : "hardware";
        SetBusy(true, $"Refreshing {capability.ToLowerInvariant()} providers…");
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
            ? $"{capability} provider responded after a clean refresh."
            : capability switch
            {
                "Sensors" => "Sensor provider is still empty after a clean LHM/PawnIO recycle. PawnIO is not reinstalled when Windows already reports it installed; the next device report will include this provider failure for debugging.",
                "Fans" => "Fan provider is still unavailable after a clean PawnIO/EC recycle. ThinkControl keeps Lenovo firmware in control rather than guessing EC writes.",
                "Keyboard" => "Keyboard provider still did not pass Lenovo readback. Check Lenovo platform/hotkey updates only after this clean retry fails.",
                _ => "Provider refresh completed, but no hardware capability passed readback yet."
            };
        SetBusy(false);
    }

    private void Continue_Click(object sender, RoutedEventArgs e) => Close();

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
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
