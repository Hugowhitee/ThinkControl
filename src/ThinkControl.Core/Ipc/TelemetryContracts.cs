namespace ThinkControl.Core.Ipc;

public static class ThinkControlProtocol
{
    public const int Version = 1;
    public const string PipeName = "ThinkControl.Service.v1";
}

public sealed record ServiceRequest(
    int Version,
    string Operation,
    string? Value = null);

public sealed record ServiceResponse(
    int Version,
    bool Success,
    string? Error = null,
    TelemetrySnapshot? Telemetry = null,
    HardwareCapabilitySnapshot? Capabilities = null);

public sealed record FanTelemetrySnapshot(
    string Id,
    string Label,
    int Rpm,
    string Source,
    bool Primary = false);

public sealed record HardwareSensorSnapshot(
    string Id,
    string HardwareName,
    string HardwareType,
    string Name,
    string SensorType,
    double Value,
    string Unit,
    bool ControlTemperature,
    string Source);

public sealed record TelemetrySnapshot(
    double? CpuTemperatureC,
    string? CpuTemperatureSource,
    int? FanRpm,
    string? FanRpmSource,
    string FanState,
    string HardwareAccess,
    string KeyboardBacklight,
    string? ThermalSolutionVersion = null,
    IReadOnlyList<FanTelemetrySnapshot>? Fans = null,
    IReadOnlyList<HardwareSensorSnapshot>? Sensors = null,
    double? ControlTemperatureC = null,
    string? ControlTemperatureSource = null);

public sealed record HardwareCapabilitySnapshot(
    bool FanTelemetry,
    bool FanControl,
    bool KeyboardBacklight,
    bool CpuTemperature,
    bool SensorTelemetry = false,
    int FanCount = 0);
