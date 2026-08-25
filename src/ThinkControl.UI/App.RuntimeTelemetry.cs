using System.Windows.Threading;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

/// <summary>
/// Owns the runtime bridge between the privileged hardware service and AppState.
/// Keeping this in one place prevents individual pages from inventing their own
/// polling loops or accidentally dropping parts of a telemetry snapshot.
/// </summary>
public partial class App
{
    public App()
    {
        HardwareClient.StatusObserved += HardwareClient_StatusObserved;
    }

    private void HardwareClient_StatusObserved(object? sender, ServiceResponse? response)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => ApplyHardwareSnapshot(response)), DispatcherPriority.Background);
            return;
        }

        ApplyHardwareSnapshot(response);
    }

    private void ApplyHardwareSnapshot(ServiceResponse? response)
    {
        if (response?.Success == true && response.Telemetry is not null)
        {
            TelemetrySnapshot telemetry = response.Telemetry;
            State.ControlTemperatureC = telemetry.ControlTemperatureC;
            State.ControlTemperatureSource = telemetry.ControlTemperatureSource ?? "Unavailable";
            State.ApplyHardwareTelemetry(telemetry.Fans, telemetry.Sensors);

            if (response.Capabilities is HardwareCapabilitySnapshot capabilities)
            {
                // The original UI bridge copied four capability flags but forgot the
                // sensor flag entirely. Fans/Sensors were also never copied into the
                // observable collections, so a healthy service still rendered empty
                // hardware pages. Keep all capability/collection state atomic here.
                State.CanSensorTelemetry = capabilities.SensorTelemetry;
                State.CanFanTelemetry = capabilities.FanTelemetry;
                State.CanFanControl = capabilities.FanControl;
                State.CanKeyboardBacklight = capabilities.KeyboardBacklight;
                State.CanCpuTemperature = capabilities.CpuTemperature;
            }
            else
            {
                State.CanSensorTelemetry = State.Sensors.Count > 0;
            }

            return;
        }

        State.ControlTemperatureC = null;
        State.ControlTemperatureSource = "Unavailable";
        State.CanSensorTelemetry = false;
        State.ClearHardwareTelemetry();
    }
}
