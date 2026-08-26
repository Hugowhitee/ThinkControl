using System.Windows;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class HardwareSetupWindow
{
    /// <summary>
    /// Applies a deterministic provider state to the real Hardware Setup controls
    /// for visual QA. No service, driver, network or UAC operation is performed.
    /// </summary>
    internal void PrepareForSnapshot(HardwareSetupStatus status)
    {
        bool serviceReady = status.ServiceRunning && status.ServiceReachable;
        bool pawnIoRepair = IsPawnIoRepairRecommended(status);

        ServiceStatusText.Text = status.ServiceDetail;
        ServiceStatusText.Foreground = (Brush)FindResource(serviceReady ? "Tc.Success" : "Tc.Warning");
        RepairServiceButton.Visibility = serviceReady ? Visibility.Collapsed : Visibility.Visible;

        LowLevelCard.Visibility = status.LowLevelAccessRelevant ? Visibility.Visible : Visibility.Collapsed;
        LowLevelStatusText.Text = pawnIoRepair && status.LowLevelAccessInstalled
            ? "Installed, but it is not ready for ThinkControl yet."
            : status.LowLevelAccessDetail;
        LowLevelStatusText.Foreground = (Brush)FindResource(status.LowLevelAccessInstalled && !pawnIoRepair ? "Tc.Success" : "Tc.Warning");
        InstallLowLevelButton.Content = status.LowLevelAccessInstalled ? "Repair PawnIO" : "Install PawnIO";
        InstallLowLevelButton.Visibility = status.LowLevelAccessRelevant && (!status.LowLevelAccessInstalled || pawnIoRepair)
            ? Visibility.Visible
            : Visibility.Collapsed;

        SensorProviderStatusText.Text = _app.State.CanSensorTelemetry
            ? $"Ready · {_app.State.SensorCountText}"
            : pawnIoRepair ? "Waiting for hardware access to be repaired."
            : "No readings yet. Recheck tries one clean provider refresh.";
        SensorProviderStatusText.Foreground = (Brush)FindResource(_app.State.CanSensorTelemetry ? "Tc.Success" : "Tc.Warning");
        RetrySensorsButton.IsEnabled = serviceReady && status.LowLevelAccessInstalled && !pawnIoRepair;
        RetrySensorsButton.Visibility = _app.State.CanSensorTelemetry ? Visibility.Collapsed : Visibility.Visible;

        bool verifiedX9 = _app.State.MachineType is "21Q6" or "21Q7";
        FanProviderStatusText.Text = _app.State.CanFanControl
            ? $"Ready · {_app.State.FanRpmText} · verified control"
            : _app.State.CanFanTelemetry
                ? "Fan readings are ready; manual control has not been verified."
                : "Firmware stays in control until this check passes.";
        FanProviderStatusText.Foreground = (Brush)FindResource(_app.State.CanFanControl ? "Tc.Success" : "Tc.Warning");
        RetryFanButton.IsEnabled = serviceReady && (!status.LowLevelAccessRelevant || (status.LowLevelAccessInstalled && !pawnIoRepair));
        RetryFanButton.Visibility = _app.State.CanFanControl ? Visibility.Collapsed : Visibility.Visible;

        LenovoDriverStatusText.Text = _app.State.CanKeyboardBacklight
            ? $"Ready · {_app.State.KeyboardStatus}"
            : "Keyboard control is not ready yet. Recheck is safe.";
        LenovoDriverStatusText.Foreground = (Brush)FindResource(_app.State.CanKeyboardBacklight ? "Tc.Success" : "Tc.Warning");
        RetryKeyboardButton.IsEnabled = serviceReady;
        RetryKeyboardButton.Visibility = _app.State.CanKeyboardBacklight ? Visibility.Collapsed : Visibility.Visible;
        OpenVantageButton.Visibility = _app.State.CanKeyboardBacklight ? Visibility.Collapsed : Visibility.Visible;

        var attention = new List<string>();
        if (!serviceReady) attention.Add("service");
        if (status.LowLevelAccessRelevant && pawnIoRepair) attention.Add("hardware access");
        if (!_app.State.CanSensorTelemetry) attention.Add("sensors");
        if (verifiedX9 && !_app.State.CanFanControl) attention.Add("fan control");
        if (!_app.State.CanKeyboardBacklight) attention.Add("keyboard controls");
        OverallStatusText.Foreground = (Brush)FindResource(attention.Count == 0 ? "Tc.Success" : "Tc.Warning");
        OverallStatusText.Text = attention.Count == 0
            ? "Ready · all controls detected for this PC passed their safety checks."
            : "Needs attention · Fix issues uses the recommended safe repair order.";
        FixDetectedIssuesButton.Content = attention.Count == 0 ? "Check again" : "Fix issues";
        ResultText.Text = attention.Count == 0
            ? "Everything expected for this device is ready. You can close this window."
            : "Fix issues repairs only what needs it, then checks the result once. Individual Recheck buttons are for a targeted retry.";
    }
}
