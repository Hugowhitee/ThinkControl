using ThinkControl.Core.Diagnostics;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    public App()
    {
        InitializeSingleInstanceGuard();
        UiMotionService.Enable();
        HardwareClient.HardwareOperationCompleted += HardwareClient_HardwareOperationCompleted;
        HardwareClient.StatusObserved += HardwareClient_StatusObserved;
        PowerModeService.ModeApplied += PowerModeService_ModeApplied;
        InitializePowerProfileCoordinator();
        InitializeCoolingCoordinator();
        InitializeAttentionNotifications();
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
                TelemetrySnapshot telemetry = response.Telemetry;
                State.HardwareAccess = telemetry.HardwareAccess;
                State.CpuTemperatureC = telemetry.CpuTemperatureC;
                State.ControlTemperatureC = telemetry.ControlTemperatureC;
                State.ControlTemperatureSource = telemetry.ControlTemperatureSource ?? "Unavailable";
                State.FanRpm = telemetry.FanRpm;
                State.FanStateText = telemetry.FanState;
                State.KeyboardStatus = telemetry.KeyboardBacklight;
                if (!string.IsNullOrWhiteSpace(telemetry.ThermalSolutionVersion))
                    State.ThermalSolution = telemetry.ThermalSolutionVersion!;

                State.ApplyHardwareTelemetry(telemetry.Fans, telemetry.Sensors);

                if (response.Capabilities is HardwareCapabilitySnapshot capabilities)
                {
                    State.CanSensorTelemetry = capabilities.SensorTelemetry;
                    State.CanFanTelemetry = capabilities.FanTelemetry;
                    State.CanFanControl = capabilities.FanControl;
                    State.CanKeyboardBacklight = capabilities.KeyboardBacklight;
                    State.CanCpuTemperature = capabilities.CpuTemperature;
                }
                else
                {
                    State.CanSensorTelemetry = State.Sensors.Count > 0;
                    State.CanFanTelemetry = State.Fans.Count > 0;
                }

                string profile = telemetry.CoolingProfile;
                if (!string.IsNullOrWhiteSpace(profile) && !profile.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase))
                {
                    State.FanStateText = telemetry.CoolingAppliedLevel is int level
                        ? $"{profile} · level {level}"
                        : profile;
                }

                _ = TryRestoreCoolingPreferenceAsync(response);
                return;
            }

            State.ControlTemperatureC = null;
            State.ControlTemperatureSource = "Unavailable";
            State.CpuTemperatureC = null;
            State.FanRpm = null;
            State.CanSensorTelemetry = false;
            State.CanFanTelemetry = false;
            State.CanFanControl = false;
            State.CanKeyboardBacklight = false;
            State.CanCpuTemperature = false;
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
            "SetCoolingProfile" => ("fan.cooling_profile_set", "FanControl", "FanSupervisor"),
            "StartFanCharacterization" => ("fan.characterization_started", "FanControl", "FanSupervisor"),
            "MarkFanLevelAudible" => ("fan.audible_level_marked", "FanControl", "FanSupervisor"),
            "StopFanCharacterization" => ("fan.characterization_stopped", "FanControl", "FanSupervisor"),
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
