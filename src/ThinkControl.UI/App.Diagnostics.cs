using ThinkControl.Core.Diagnostics;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    public App() : this(enforceSingleInstance: true)
    {
    }

    private App(bool enforceSingleInstance)
    {
        BatteryHistoryService = new BatteryHistoryService(UserSettings.Current.BatteryDetailRetentionDays);
        if (enforceSingleInstance)
            InitializeSingleInstanceGuard();
        UiMotionService.Enable();
        HardwareClient.HardwareOperationCompleted += HardwareClient_HardwareOperationCompleted;
        HardwareClient.StatusObserved += HardwareClient_StatusObserved;
        PowerModeService.ModeApplied += PowerModeService_ModeApplied;
        InitializePowerProfileCoordinator();
        InitializeCoolingCoordinator();
        InitializeAttentionNotifications();
        if (enforceSingleInstance)
            InitializeDiagnosticsLifecycle();
        Startup += OnShellIconStartup;
        Activated += OnTouchpadApplicationActivated;
        Activated += OnHardwareSetupActivated;
        Exit += OnTouchpadApplicationExit;
    }

    /// <summary>
    /// Creates the real WPF application resources for deterministic rendering
    /// without treating the renderer as a second desktop launch. Startup is not
    /// raised by the snapshot host, so tray, polling and hardware work stay idle.
    /// </summary>
    public static App CreateForVisualQa() => new(enforceSingleInstance: false);

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
                State.CoolingProfile = telemetry.CoolingProfile;
                State.KeyboardStatus = telemetry.KeyboardBacklight;
                State.KeyboardBackend = telemetry.KeyboardBackend ?? "Not exposed";
                if (!string.IsNullOrWhiteSpace(telemetry.ThermalSolutionVersion))
                    State.ThermalSolution = telemetry.ThermalSolutionVersion!;

                State.ApplyHardwareTelemetry(telemetry.Fans, telemetry.Sensors);
                if (State.BatteryTemperatureC is null)
                    State.BatteryTemperatureC = ResolveCredibleBatteryTemperature(State.Sensors);

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
                        ? $"{profile} · EC level {level}"
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

    private static double? ResolveCredibleBatteryTemperature(IEnumerable<HardwareSensorSnapshot> sensors)
    {
        HardwareSensorSnapshot? reading = sensors
            .Where(sensor => sensor.SensorType.Equals("Temperature", StringComparison.OrdinalIgnoreCase))
            .Where(sensor => sensor.Value is >= 0 and <= 70)
            .Where(sensor => ContainsBatteryIdentity(sensor.HardwareName) ||
                             ContainsBatteryIdentity(sensor.Name) ||
                             ContainsBatteryIdentity(sensor.Source))
            .OrderByDescending(sensor => ContainsBatteryIdentity(sensor.HardwareName))
            .ThenByDescending(sensor => ContainsBatteryIdentity(sensor.Name))
            .FirstOrDefault();
        return reading is null ? null : Math.Round(reading.Value, 1);
    }

    private static bool ContainsBatteryIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Contains("battery", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("batt", StringComparison.OrdinalIgnoreCase));

    private void HardwareClient_HardwareOperationCompleted(object? sender, HardwareOperationResult operation)
    {
        (string eventName, string capability, string provider) = operation.Operation switch
        {
            "SetFanLevel" => ("fan.level_set", "FanControl", "ThinkPadEC"),
            "ReturnFanToAuto" => ("fan.returned_to_auto", "FanControl", "ThinkPadEC"),
            "SetCoolingCurve" => ("fan.cooling_curve_set", "FanControl", "FanSupervisor"),
            "StartFanCharacterization" => ("fan.characterization_started", "FanControl", "FanSupervisor"),
            "StopFanCharacterization" => ("fan.characterization_stopped", "FanControl", "FanSupervisor"),
            "SetKeyboardBacklight" => ("keyboard.level_set", "KeyboardBacklight", "Lenovo"),
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
