namespace ThinkControl.Core.Capabilities;

public enum CapabilityId
{
    PerformanceMode,
    LenovoThermalPolicy,
    CpuTemperature,
    FanRpm,
    FanControl,
    KeyboardBacklight,
    DisplayRefresh,
    AdaptiveBrightness,
    BatteryTelemetry,
    BatteryChargeThreshold
}

public enum CapabilitySupport
{
    Unavailable,
    SafeReadOnly,
    ExperimentalReadOnly,
    Verified,
    BlockedByConflict
}

public sealed record Capability(
    CapabilityId Id,
    CapabilitySupport Support,
    string Provider,
    string? Detail = null);
