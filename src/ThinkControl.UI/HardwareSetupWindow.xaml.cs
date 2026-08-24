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
        bool needsSensorProvider = !_app.State.CanSensorTelemetry || !_app.State.CanFanTelemetry;
        HardwareSetupStatus status = await _service.ReadStatusAsync(_app.State.MachineType, needsSensorProvider);
        ServiceStatusText.Text = status.ServiceDetail;
        ServiceStatusText.Foreground = (Brush)FindResource(status.ServiceRunning ? "Tc.Success" : "Tc.Warning");
        RepairServiceButton.Visibility = status.ServiceRunning ? Visibility.Collapsed : Visibility.Visible;

        LowLevelCard.Visibility = status.LowLevelAccessRelevant ? Visibility.Visible : Visibility.Collapsed;
        LowLevelStatusText.Text = status.LowLevelAccessDetail;
        LowLevelStatusText.Foreground = (Brush)FindResource(status.LowLevelAccessInstalled ? "Tc.Success" : "Tc.Warning");
        InstallLowLevelButton.Visibility = status.LowLevelAccessInstalled ? Visibility.Collapsed : Visibility.Visible;

        bool lenovoDevice = _app.State.DeviceName.Contains("ThinkPad", StringComparison.OrdinalIgnoreCase) ||
                            _app.State.DeviceName.Contains("Lenovo", StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrWhiteSpace(_app.State.MachineType) && _app.State.MachineType != "—");
        LenovoDriverCard.Visibility = lenovoDevice && !_app.State.CanKeyboardBacklight
            ? Visibility.Visible
            : Visibility.Collapsed;
        LenovoDriverStatusText.Text = _app.State.CanKeyboardBacklight
            ? "Keyboard provider detected"
            : "Keyboard provider not detected yet";
        LenovoDriverStatusText.Foreground = (Brush)FindResource(
            _app.State.CanKeyboardBacklight ? "Tc.Success" : "Tc.Warning");

        await _app.RefreshHardwareSetupStatusAsync();

        if (!status.NeedsAttention && _app.State.CanKeyboardBacklight && string.IsNullOrWhiteSpace(ResultText.Text))
            ResultText.Text = "Hardware providers are ready. ThinkControl will keep re-detecting sensors and supported controls automatically.";
        else if (!status.NeedsAttention && string.IsNullOrWhiteSpace(ResultText.Text))
            ResultText.Text = "Core hardware access is ready. Optional Lenovo keyboard integration can be repaired separately if needed.";
    }

    private async void RepairService_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;
        SetBusy(true, "Repairing the ThinkControl hardware service...");
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
        SetBusy(true, "Downloading and SHA-256 verifying PawnIO...");
        HardwareSetupResult result = await _service.InstallLowLevelAccessAsync();
        ResultText.Text = result.Message;
        await _app.RefreshStatusAsync(forceSystemInfo: true);
        await RefreshAsync();
        SetBusy(false);
    }

    private void OpenVantage_Click(object sender, RoutedEventArgs e)
    {
        if (LenovoSoftwareLauncher.TryOpenVantage())
        {
            ResultText.Text = "Lenovo Vantage opened. Install the recommended Lenovo system/interface updates, then return here and press Retry.";
            return;
        }

        ResultText.Text = "Lenovo Vantage is not registered on this Windows installation. Install Lenovo Vantage, run Windows Update including optional Lenovo updates, then retry detection.";
    }

    private async void RetryDetection_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;
        SetBusy(true, "Re-detecting hardware providers...");
        await _app.RefreshStatusAsync(forceSystemInfo: true);
        await RefreshAsync();
        ResultText.Text = _app.State.CanKeyboardBacklight
            ? "Keyboard provider detected."
            : "Keyboard provider is still unavailable. Lenovo Vantage / Windows Update may have a platform-driver update for this device.";
        SetBusy(false);
    }

    private void Continue_Click(object sender, RoutedEventArgs e) => Close();

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        RepairServiceButton.IsEnabled = !busy;
        InstallLowLevelButton.IsEnabled = !busy;
        OpenVantageButton.IsEnabled = !busy;
        RetryDetectionButton.IsEnabled = !busy;
        if (!string.IsNullOrWhiteSpace(message))
            ResultText.Text = message;
    }
}
