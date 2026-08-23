namespace ThinkControl.Core.Diagnostics;

public enum DeviceValidationState
{
    Verified,
    Experimental,
    NotValidated
}

public enum DiagnosticsConsent
{
    Unknown,
    Disabled,
    Enabled
}

public sealed record DiagnosticDeviceInfo(
    string Manufacturer,
    string ProductName,
    string? MachineType,
    string? BiosVersion,
    DeviceValidationState ValidationState);

public sealed record DiagnosticEvent(
    DateTimeOffset TimestampUtc,
    string Name,
    string? Capability = null,
    string? Provider = null,
    DeviceValidationState ValidationState = DeviceValidationState.NotValidated,
    bool? Success = null,
    string? ErrorCode = null,
    int? DurationMs = null,
    bool? ReadBackVerified = null,
    int? FanLevel = null,
    int? FanRpm = null,
    double? TemperatureC = null,
    IReadOnlyDictionary<string, string>? Tags = null);

public sealed record DiagnosticBundle(
    int SchemaVersion,
    string ThinkControlVersion,
    string Channel,
    string WindowsVersion,
    DiagnosticDeviceInfo Device,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<DiagnosticEvent> Events);

public static class DiagnosticsPolicy
{
    public const int SchemaVersion = 1;
    public const int MaximumEventsPerBundle = 500;

    public static readonly IReadOnlySet<string> AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "operation",
        "state",
        "source",
        "dependency",
        "dependencyVersion",
        "resumeState",
        "conflict",
        "windowsBuild"
    };
}
