using System.Windows;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class HardwareSetupWindow : Window
{
    private readonly App _app;
    private readonly HardwareSetupService _service;
    private readonly HardwarePrerequisiteIssue _requestedIssue;
    private HardwarePrerequisiteIssue _currentIssue;
    private bool _busy;
    private bool _closeOnPrimaryAction;
    private CancellationTokenSource? _autoClose;

    internal HardwareSetupWindow(
        App app,
        HardwareSetupService service,
        HardwarePrerequisiteIssue issue = HardwarePrerequisiteIssue.Auto)
    {
        _app = app;
        _service = service;
        _requestedIssue = issue;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
        Closed += (_, _) =>
        {
            try { _autoClose?.Cancel(); } catch { }
            _autoClose?.Dispose();
            _autoClose = null;
        };
    }

    private async Task RefreshAsync()
    {
        HardwareSetupStatus status = await _app.RefreshHardwareSetupStatusAsync();
        PresentStatus(status);
    }

    private void PresentStatus(HardwareSetupStatus status)
    {
        _currentIssue = _requestedIssue == HardwarePrerequisiteIssue.Auto
            ? _app.ResolvePrimaryHardwareIssue(status)
            : _requestedIssue;

        if (IsIssueReady(_currentIssue, status))
        {
            ShowSuccess();
            return;
        }

        _closeOnPrimaryAction = false;
        CancelButton.Visibility = Visibility.Visible;
        PrimaryActionButton.Visibility = Visibility.Visible;
        ResultText.Text = string.Empty;
        SetStatusIcon(IconFor(_currentIssue), "Tc.Warning");

        switch (_currentIssue)
        {
            case HardwarePrerequisiteIssue.Service:
                PrimaryTitleText.Text = "ThinkControl service needs repair";
                PrimaryStatusText.Text = "ThinkControl uses its background service for hardware controls. Windows will ask once to repair and start it; current settings are left unchanged.";
                PrimaryActionButton.Content = "Repair service";
                break;
            case HardwarePrerequisiteIssue.PawnIo:
                bool registered = status.LowLevelAccessRegistered;
                PrimaryTitleText.Text = registered ? "PawnIO needs repair" : "PawnIO installation required";
                PrimaryStatusText.Text = registered
                    ? "ThinkControl found an incomplete or unusable PawnIO installation. Repair restores the verified driver required for fan control and sensor data; other Windows controls continue to work."
                    : "ThinkControl needs this verified driver for fan control and sensor data on this PC. Other Windows controls continue to work without it.";
                PrimaryActionButton.Content = registered ? "Repair PawnIO" : "Install PawnIO";
                break;
            case HardwarePrerequisiteIssue.Sensors:
                PrimaryTitleText.Text = "Sensor provider needs a retry";
                PrimaryStatusText.Text = "ThinkControl has not received stable sensor readings. This retries only sensor discovery and does not change fan or keyboard settings.";
                PrimaryActionButton.Content = "Retry sensors";
                break;
            case HardwarePrerequisiteIssue.FanControl:
                PrimaryTitleText.Text = "Fan provider needs a retry";
                PrimaryStatusText.Text = "ThinkControl has not verified the supported fan path. Firmware remains safely in control while the provider is refreshed and checked again.";
                PrimaryActionButton.Content = "Retry fan provider";
                break;
            case HardwarePrerequisiteIssue.Keyboard:
                PrimaryTitleText.Text = "Keyboard provider needs a retry";
                PrimaryStatusText.Text = "ThinkControl has not received a valid Lenovo keyboard readback. This retry does not recycle working fan or sensor providers.";
                PrimaryActionButton.Content = "Retry keyboard";
                break;
            default:
                ShowSuccess();
                break;
        }
    }

    private async void PrimaryAction_Click(object sender, RoutedEventArgs e)
    {
        if (_closeOnPrimaryAction)
        {
            Close();
            return;
        }
        if (_busy)
            return;

        SetBusy(true);
        try
        {
            bool success;
            bool restartRequired = false;
            string failure;
            switch (_currentIssue)
            {
                case HardwarePrerequisiteIssue.Service:
                    HardwareSetupResult service = await _service.RepairServiceAsync();
                    success = service.Success;
                    restartRequired = service.RestartRequired;
                    failure = service.Message;
                    if (success)
                        await _app.RefreshStatusAsync(forceSystemInfo: true);
                    break;
                case HardwarePrerequisiteIssue.PawnIo:
                    HardwareSetupResult pawnIo = await _service.InstallLowLevelAccessAsync();
                    success = pawnIo.Success;
                    restartRequired = pawnIo.RestartRequired;
                    failure = pawnIo.Message;
                    if (success && !restartRequired)
                    {
                        bool providersReady = await _app.RefreshHardwareProvidersAsync();
                        bool expectsWritableFan = DeviceCapabilityExpectations.ExpectsWritableFanControl(_app.State);
                        if (expectsWritableFan && !_app.State.CanFanControl)
                        {
                            success = false;
                            failure = "PawnIO registration and its kernel service were repaired, but the verified X9 EC fan path still did not pass its real access/readback gate. Lenovo firmware remains in control and fan writes stay disabled; retry the fan provider or review Diagnostics.";
                        }
                        else if (!providersReady)
                        {
                            success = false;
                            failure = "PawnIO registration and its kernel service were repaired, but the hardware provider still could not open and verify the required device path. Unsafe hardware actions remain disabled; retry the provider or review Diagnostics.";
                        }
                    }
                    break;
                case HardwarePrerequisiteIssue.Sensors:
                    success = await _app.RefreshSensorProvidersAsync();
                    failure = "The sensor provider still has not produced stable telemetry.";
                    break;
                case HardwarePrerequisiteIssue.FanControl:
                    success = await _app.RefreshHardwareProvidersAsync();
                    failure = "The supported fan provider still did not pass its safety/readback checks. Firmware remains in control.";
                    break;
                case HardwarePrerequisiteIssue.Keyboard:
                    success = await _app.RefreshKeyboardProviderAsync();
                    failure = "The Lenovo keyboard provider still did not return a valid readback.";
                    break;
                default:
                    success = true;
                    failure = string.Empty;
                    break;
            }

            HardwareSetupStatus status = await _app.RefreshHardwareSetupStatusAsync();
            if (restartRequired)
            {
                ShowRestartRequired();
                return;
            }
            if (success && IsIssueReady(_currentIssue, status))
            {
                ShowSuccess();
                return;
            }

            ShowFailure(failure);
        }
        catch (Exception ex)
        {
            ShowFailure($"ThinkControl could not complete this action: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private bool IsIssueReady(HardwarePrerequisiteIssue issue, HardwareSetupStatus status) => issue switch
    {
        HardwarePrerequisiteIssue.None => true,
        HardwarePrerequisiteIssue.Service => status.ServiceRunning && status.ServiceReachable,
        HardwarePrerequisiteIssue.PawnIo => status.LowLevelAccessInstalled &&
                                             _app.ResolvePrimaryHardwareIssue(status) != HardwarePrerequisiteIssue.PawnIo &&
                                             (!DeviceCapabilityExpectations.ExpectsWritableFanControl(_app.State) || _app.State.CanFanControl),
        HardwarePrerequisiteIssue.Sensors => _app.State.CanSensorTelemetry,
        HardwarePrerequisiteIssue.FanControl => _app.State.CanFanControl,
        HardwarePrerequisiteIssue.Keyboard => _app.State.CanKeyboardBacklight,
        _ => _app.ResolvePrimaryHardwareIssue(status) == HardwarePrerequisiteIssue.None
    };

    private void ShowSuccess()
    {
        _currentIssue = HardwarePrerequisiteIssue.None;
        _closeOnPrimaryAction = false;
        PrimaryTitleText.Text = "You're all set";
        PrimaryStatusText.Text = "The required component is ready and its capability check passed.";
        ResultText.Text = "Closing automatically…";
        SetStatusIcon("Check", "Tc.Success");
        ActionRow.Visibility = Visibility.Collapsed;
        if (IsLoaded)
            ScheduleAutoClose();
    }

    private void ShowRestartRequired()
    {
        PrimaryTitleText.Text = "Restart Windows to finish";
        PrimaryStatusText.Text = "PawnIO is installed. Restart once so Windows can make the verified driver available to fan control and sensors.";
        ResultText.Text = string.Empty;
        SetStatusIcon("Check", "Tc.Success");
        ShowCloseAction();
    }

    private void ShowFailure(string detail)
    {
        PrimaryTitleText.Text = "The issue still persists";
        PrimaryStatusText.Text = string.IsNullOrWhiteSpace(detail)
            ? "ThinkControl could not verify the required capability. Unsafe hardware actions remain disabled."
            : detail;
        ResultText.Text = "You can close this message and review the current provider status in Inbox.";
        SetStatusIcon("Error", "Tc.Accent");
        ShowCloseAction();
    }

    private void ShowCloseAction()
    {
        ActionRow.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Collapsed;
        PrimaryActionButton.Visibility = Visibility.Visible;
        PrimaryActionButton.Content = "Close";
        PrimaryActionButton.IsEnabled = true;
        _closeOnPrimaryAction = true;
    }

    private async void ScheduleAutoClose()
    {
        CancellationTokenSource owner = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _autoClose, owner);
        try { previous?.Cancel(); } catch { }
        previous?.Dispose();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1.8), owner.Token);
            if (!owner.IsCancellationRequested && IsVisible)
                Close();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        CancelButton.IsEnabled = !busy;
        PrimaryActionButton.IsEnabled = !busy;
        if (busy)
            ResultText.Text = "Working…";
    }

    private void SetStatusIcon(string kind, string brushResource)
    {
        StatusIcon.Kind = kind;
        StatusIcon.SetResourceReference(ForegroundProperty, brushResource);
    }

    private static string IconFor(HardwarePrerequisiteIssue issue) => issue switch
    {
        HardwarePrerequisiteIssue.Sensors => "Sensors",
        HardwarePrerequisiteIssue.Keyboard => "Keyboard",
        HardwarePrerequisiteIssue.FanControl => "Fan",
        _ => "Cpu"
    };

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
