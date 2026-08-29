using ThinkControl.Core.Diagnostics;
using ThinkControl.Core.Ipc;

namespace ThinkControl.UI;

public partial class App
{
    private static readonly TimeSpan FanDiagnosticSampleInterval = TimeSpan.FromSeconds(6);
    private DateTimeOffset _lastFanDiagnosticSampleAt = DateTimeOffset.MinValue;
    private string _lastFanDiagnosticMode = string.Empty;

    private void RecordFanTelemetrySample(TelemetrySnapshot telemetry)
    {
        if (!string.Equals(State.MachineType, "21Q6", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(State.MachineType, "21Q7", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        FanTelemetrySnapshot[] fans = (telemetry.Fans ?? Array.Empty<FanTelemetrySnapshot>())
            .Where(fan => fan.Rpm is >= 0 and <= 20_000)
            .Take(2)
            .ToArray();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string mode = $"{telemetry.CoolingProfile}|{telemetry.CoolingAppliedLevel?.ToString() ?? "auto"}|{fans.Length}";
        bool modeChanged = !string.Equals(mode, _lastFanDiagnosticMode, StringComparison.Ordinal);
        if (!modeChanged && now - _lastFanDiagnosticSampleAt < FanDiagnosticSampleInterval)
            return;

        _lastFanDiagnosticSampleAt = now;
        _lastFanDiagnosticMode = mode;

        var tags = new Dictionary<string, string>
        {
            ["profile"] = string.IsNullOrWhiteSpace(telemetry.CoolingProfile) ? "unknown" : telemetry.CoolingProfile,
            ["state"] = telemetry.FanState
        };
        if (fans.Length > 0)
            tags["fan1Rpm"] = fans[0].Rpm.ToString();
        if (fans.Length > 1)
            tags["fan2Rpm"] = fans[1].Rpm.ToString();

        RecordDiagnostic(new DiagnosticEvent(
            now,
            "fan.telemetry_sample",
            Capability: "FanTelemetry",
            Provider: fans.FirstOrDefault()?.Source ?? telemetry.FanRpmSource ?? "Unavailable",
            ValidationState: GetCurrentDeviceValidationState(),
            Success: fans.Length > 0 || telemetry.FanRpm.HasValue,
            FanLevel: telemetry.CoolingAppliedLevel,
            FanRpm: fans.FirstOrDefault()?.Rpm ?? telemetry.FanRpm,
            TemperatureC: telemetry.ControlTemperatureC,
            Tags: tags));
    }
}
