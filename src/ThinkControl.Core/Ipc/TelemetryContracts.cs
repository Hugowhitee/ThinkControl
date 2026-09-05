namespace ThinkControl.Core.Ipc;

public static class ThinkControlProtocol
{
    public const int Version = 1;
    public const string PipeName = "ThinkControl.Service.v1";
}

public static class FanControlKinds
{
    public const string None = "None";
    public const string DiscreteEc = "ThinkPadEcDiscrete";
    public const string OemTargetRpm = "LenovoOtherModeTargetRpm";
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

public sealed record FanCalibrationFanSnapshot(
    string Id,
    string Label,
    int MedianRpm,
    int SpreadRpm,
    bool Stable);

public sealed record FanLevelCalibrationSnapshot(
    int Level,
    IReadOnlyList<FanCalibrationFanSnapshot> Fans,
    bool Stable);

public sealed record FanCharacterizationSnapshot(
    bool Running,
    int? CurrentLevel,
    int CompletedLevels,
    int TotalLevels,
    string Status,
    int? AudibleFromLevel,
    IReadOnlyList<FanLevelCalibrationSnapshot> Levels);

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
    string? ControlTemperatureSource = null,
    string CoolingProfile = "Lenovo Auto",
    int? CoolingAppliedLevel = null,
    double? CoolingSmoothedTemperatureC = null,
    string CoolingStatus = "Lenovo firmware owns fan control",
    bool CoolingSafetyOverride = false,
    FanCharacterizationSnapshot? FanCharacterization = null,
    string? CoolingProfileId = null,
    int? CoolingAppliedPercent = null,
    string? KeyboardBackend = null);

public sealed record HardwareCapabilitySnapshot(
    bool FanTelemetry,
    bool FanControl,
    bool KeyboardBacklight,
    bool CpuTemperature,
    bool SensorTelemetry = false,
    int FanCount = 0,
    string FanControlKind = FanControlKinds.None,
    bool FanCalibrationSupported = false,
    bool FanCalibrationRequired = false,
    bool KeyboardEffects = false);
