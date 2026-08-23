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
        HardwareSetupStatus status = await _service.ReadStatusAsync(_app.State.MachineType);
        ServiceStatusText.Text = status.ServiceDetail;
        ServiceStatusText.Foreground = (Brush)FindResource(status.ServiceRunning ? "Tc.Success" : "Tc.Warning");
        RepairServiceButton.Visibility = status.ServiceRunning ? Visibility.Collapsed : Visibility.Visible;

        LowLevelCard.Visibility = status.LowLevelAccessRelevant ? Visibility.Visible : Visibility.Collapsed;
        LowLevelStatusText.Text = status.LowLevelAccessDetail;
        LowLevelStatusText.Foreground = (Brush)FindResource(status.LowLevelAccessInstalled ? "Tc.Success" : "Tc.Warning");
        InstallLowLevelButton.Visibility = status.LowLevelAccessInstalled ? Visibility.Collapsed : Visibility.Visible;

        await _app.RefreshHardwareSetupStatusAsync();

        if (!status.NeedsAttention && string.IsNullOrWhiteSpace(ResultText.Text))
            ResultText.Text = "Hardware components are ready. ThinkControl will refresh telemetry automatically.";
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
        SetBusy(true, "Downloading and verifying the hardware component...");
        HardwareSetupResult result = await _service.InstallLowLevelAccessAsync();
        ResultText.Text = result.Message;
        await _app.RefreshStatusAsync(forceSystemInfo: true);
        await RefreshAsync();
        SetBusy(false);
    }

    private void Continue_Click(object sender, RoutedEventArgs e) => Close();

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        RepairServiceButton.IsEnabled = !busy;
        InstallLowLevelButton.IsEnabled = !busy;
        if (!string.IsNullOrWhiteSpace(message))
            ResultText.Text = message;
    }
}
