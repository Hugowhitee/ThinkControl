using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    public App()
    {
        HardwareClient.HardwareOperationCompleted += HardwareClient_HardwareOperationCompleted;
    }

    private void HardwareClient_HardwareOperationCompleted(object? sender, HardwareOperationResult operation)
    {
        string eventName = operation.Operation switch
        {
            "SetFanLevel" => "fan.level_set",
            "ReturnFanToAuto" => "fan.returned_to_auto",
            _ => "hardware.operation"
        };

        int? level = operation.Operation == "SetFanLevel" && int.TryParse(operation.Value, out int parsed)
            ? parsed
            : null;

        RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            eventName,
            Capability: "FanControl",
            Provider: "ThinkPadEC",
            ValidationState: GetDeviceValidationState(State.MachineType),
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
                ["state"] = operation.Operation == "ReturnFanToAuto" ? "LenovoAuto" : operation.Value ?? "unknown"
            }));
    }
}
