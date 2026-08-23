using ThinkControl.Core.Diagnostics;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    public App()
    {
        HardwareClient.HardwareOperationCompleted += HardwareClient_HardwareOperationCompleted;
        HardwareClient.StatusObserved += HardwareClient_StatusObserved;
        PowerModeService.ModeApplied += PowerModeService_ModeApplied;
        Startup += OnBootstrapStartup;
        Startup += OnShellIconStartup;
        Activated += OnTouchpadApplicationActivated;
        Activated += OnHardwareSetupActivated;
        Exit += OnTouchpadApplicationExit;
    }

    private void HardwareClient_StatusObserved(object? sender, ServiceResponse? response)
    {
        void Apply()
        {
            if (response?.Success == true && response.Telemetry is not null)
            {
                State.ControlTemperatureC = response.Telemetry.ControlTemperatureC;
                State.ControlTemperatureSource = response.Telemetry.ControlTemperatureSource ?? "Unavailable";
                State.ApplyHardwareTelemetry(response.Telemetry.Fans, response.Telemetry.Sensors);
                if (response.Capabilities is not null)
                    State.CanSensorTelemetry = response.Capabilities.SensorTelemetry;
                return;
            }

            State.ControlTemperatureC = null;
            State.ControlTemperatureSource = "Unavailable";
            State.CanSensorTelemetry = false;
            State.ClearHardwareTelemetry();
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.BeginInvoke(Apply);
    }

    private void HardwareClient_HardwareOperationCompleted(object? sender, HardwareOperationResult operation)
    {
        (string eventName, string capability, string provider) = operation.Operation switch
        {
            "SetFanLevel" => ("fan.level_set", "FanControl", "ThinkPadEC"),
            "ReturnFanToAuto" => ("fan.returned_to_auto", "FanControl", "ThinkPadEC"),
            "SetThermalMode" => ("thermal.policy_set", "ThermalPolicy", "LenovoLITS"),
            _ => ("hardware.operation", "Hardware", "ThinkControlService")
        };

        int? level = operation.Operation == "SetFanLevel" && int.TryParse(operation.Value, out int parsed)
            ? parsed
            : null;

        string state = operation.Operation == "ReturnFanToAuto"
            ? "LenovoAuto"
            : operation.Value ?? "unknown";

        RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            eventName,
            Capability: capability,
            Provider: provider,
            ValidationState: GetDeviceValidationState(State.MachineType, _manufacturer, State.DeviceName),
            Success: operation.Success,
            ErrorCode: operation.Success
                ? null
                : operation.ResponseReceived ? "operation_rejected" : "service_no_response",
            DurationMs: operation.DurationMs,
            ReadBackVerified: operation.Success,
            FanLevel: level,
            Tags: new Dictionary<string, string>
            {
                ["operation"] = operation.Operation,
                ["state"] = state
            }));
    }
}
