namespace ThinkControl.Core.Dependencies;

public static class DependencyCatalog
{
    private static readonly IReadOnlyDictionary<DependencyId, DependencyDefinition> Definitions =
        new Dictionary<DependencyId, DependencyDefinition>
        {
            [DependencyId.DotNetDesktopRuntime] = new(
                DependencyId.DotNetDesktopRuntime,
                ".NET 10 Desktop Runtime",
                DependencyRequirement.Required,
                "Runs the ThinkControl WPF application and managed service."),
            [DependencyId.ThinkControlService] = new(
                DependencyId.ThinkControlService,
                "ThinkControl Hardware Service",
                DependencyRequirement.Required,
                "Owns privileged hardware operations and background profile enforcement."),
            [DependencyId.PawnIo] = new(
                DependencyId.PawnIo,
                "PawnIO hardware access",
                DependencyRequirement.DeviceConditional,
                "Provides verified low-level access for EC telemetry, fan control and selected sensors."),
            [DependencyId.LenovoIntelligentThermalSolution] = new(
                DependencyId.LenovoIntelligentThermalSolution,
                "Lenovo Intelligent Thermal Solution",
                DependencyRequirement.OemPlatform,
                "Provides Lenovo Intelligent Cooling and thermal-policy integration when supported."),
            [DependencyId.LenovoPowerManagement] = new(
                DependencyId.LenovoPowerManagement,
                "Lenovo Power Management",
                DependencyRequirement.OemPlatform,
                "Provides Lenovo PM/ACPI bridge functionality used by verified ThinkPad-specific controls."),
            [DependencyId.IntelInnovationPlatformFramework] = new(
                DependencyId.IntelInnovationPlatformFramework,
                "Intel Innovation Platform Framework",
                DependencyRequirement.OemPlatform,
                "Participates in the Windows/OEM energy and thermal policy stack."),
            [DependencyId.LenovoVantage] = new(
                DependencyId.LenovoVantage,
                "Lenovo Vantage / Commercial Vantage",
                DependencyRequirement.OptionalIntegration,
                "Optional Lenovo maintenance, warranty, update and support companion."),
            [DependencyId.LenovoServiceBridge] = new(
                DependencyId.LenovoServiceBridge,
                "Lenovo Service Bridge",
                DependencyRequirement.OptionalIntegration,
                "Optional Lenovo Support product-detection helper.")
        };

    public static DependencyDefinition Get(DependencyId id) => Definitions[id];

    public static IReadOnlyCollection<DependencyDefinition> All => Definitions.Values;
}
