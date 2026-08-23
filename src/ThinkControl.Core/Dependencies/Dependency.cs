namespace ThinkControl.Core.Dependencies;

public enum DependencyId
{
    DotNetDesktopRuntime,
    ThinkControlService,
    PawnIo,
    LenovoIntelligentThermalSolution,
    LenovoPowerManagement,
    IntelInnovationPlatformFramework,
    LenovoVantage,
    LenovoServiceBridge
}

public enum DependencyRequirement
{
    Required,
    DeviceConditional,
    OemPlatform,
    OptionalIntegration
}

public enum DependencyState
{
    Unknown,
    Present,
    Missing,
    Outdated,
    NeedsRestart,
    Unhealthy
}

public sealed record DependencyDefinition(
    DependencyId Id,
    string DisplayName,
    DependencyRequirement Requirement,
    string Purpose);

public sealed record DependencyStatus(
    DependencyDefinition Definition,
    DependencyState State,
    string? Version = null,
    string? Detail = null)
{
    public bool IsReady => State == DependencyState.Present;
}

public enum HardwareReadiness
{
    Full,
    Limited,
    NeedsAttention
}
