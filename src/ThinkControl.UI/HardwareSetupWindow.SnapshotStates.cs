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

        // The healthy setup image is also used inside the public landscape release
        // overview. Render that one state in a proportional wide viewport instead of
        // stretching a nearly-square window in the collage. Repair/minimum snapshots
        // retain their dedicated 700x720 and 600x580 coverage.
        if (serviceReady && !pawnIoRepair && _app.State.CanSensorTelemetry)
        {
            Width = 1160;
            Height = 760;
        }

        ServiceStatusText.Text = status.ServiceDetail;
        ServiceStatusText.Foreground = (Brush)FindResource(serviceReady ? "Tc.Success" : "Tc.Warning");
        RepairServiceButton.Visibility = serviceReady ? Visibility.Collapsed : Visibility.Visible;

        LowLevelCard.Visibility = status.LowLevelAccessRelevant ? Visibility.Visible : Visibility.Collapsed;
        LowLevelStatusText.Text = pawnIoRepair && status.LowLevelAccessInstalled
            ? DescribePawnIoFailure(_app.State.HardwareAccess)
            : status.LowLevelAccessDetail;
        LowLevelStatusText.Foreground = (Brush)FindResource(status.LowLevelAccessInstalled && !pawnIoRepair ? "Tc.Success" : "Tc.Warning");
        InstallLowLevelButton.Content = status.LowLevelAccessInstalled ? "Repair PawnIO" : "Install PawnIO";
        InstallLowLevelButton.Visibility = status.LowLevelAccessRelevant && (!status.LowLevelAccessInstalled || pawnIoRepair)
            ? Visibility.Visible
            : Visibility.Collapsed;

        SensorProviderStatusText.Text = _app.State.CanSensorTelemetry
            ? $"Ready · {_app.State.SensorCountText}"
            : pawnIoRepair
                ? "LibreHardwareMonitor is waiting for working PawnIO device/module access. Repair PawnIO first; repeated sensor retries would not fix this state."
                : status.LowLevelAccessInstalled
                    ? "PawnIO is present and no device/module repair is indicated, but LHM has not produced useful sensor telemetry yet. Recheck performs one clean provider rebuild."
                    : "PawnIO is missing. Fix detected issues will install the verified package before rebuilding sensors.";
        SensorProviderStatusText.Foreground = (Brush)FindResource(_app.State.CanSensorTelemetry ? "Tc.Success" : "Tc.Warning");
        RetrySensorsButton.IsEnabled = serviceReady && status.LowLevelAccessInstalled && !pawnIoRepair;

        bool verifiedX9 = _app.State.MachineType is "21Q6" or "21Q7";
        FanProviderStatusText.Text = _app.State.CanFanControl
            ? $"Ready · verified X9 EC control · {_app.State.FanRpmText} · {_app.State.HardwareAccess}"
            : _app.State.CanFanTelemetry
                ? $"Fan telemetry is available, but the verified X9 EC control/readback gate has not passed. {_app.State.HardwareAccess}"
                : verifiedX9
                    ? $"Verified X9 profile detected. Lenovo firmware remains in control until the read-only EC gate succeeds. {_app.State.HardwareAccess}"
                    : "Read-only fan telemetry can be discovered, but manual fan control is limited to verified device profiles.";
        FanProviderStatusText.Foreground = (Brush)FindResource(_app.State.CanFanControl ? "Tc.Success" : "Tc.Warning");
        RetryFanButton.IsEnabled = serviceReady && (!status.LowLevelAccessRelevant || (status.LowLevelAccessInstalled && !pawnIoRepair));

        LenovoDriverStatusText.Text = _app.State.CanKeyboardBacklight
            ? $"Ready · {_app.State.KeyboardStatus}"
            : "Lenovo keyboard readback has not passed yet. Recheck probes PM/EnergyDrv/Vantage providers once; repeated failed probes are backed off automatically.";
        LenovoDriverStatusText.Foreground = (Brush)FindResource(_app.State.CanKeyboardBacklight ? "Tc.Success" : "Tc.Warning");
        RetryKeyboardButton.IsEnabled = serviceReady;

        ResultText.Text = serviceReady
            ? pawnIoRepair
                ? "PawnIO is registered, but its device/module handshake failed. Repair PawnIO once, then ThinkControl will rebuild providers and verify readback."
                : "All required components are installed. Individual Recheck actions are available only for targeted provider diagnostics."
            : status.ServiceRunning
                ? "The Windows service is running, but the ThinkControl app cannot reach its IPC endpoint. Fix detected issues will restart and re-register the service before hardware providers are touched."
                : "Fix detected issues will repair the ThinkControl service first, then continue with hardware providers.";
    }
}
