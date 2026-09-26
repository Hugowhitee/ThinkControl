using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class ThinkControlModesSourceTests
{
    [Fact]
    public void Modes_AreSparseAndDoNotOwnUnrelatedHardwarePolicies()
    {
        string root = FindRepositoryRoot();
        string models = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeModels.cs");
        string coordinator = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeCoordinator.cs");

        Assert.Contains("AudioSafety = null", models, StringComparison.Ordinal);
        Assert.Contains("bool? TouchpadGesturesEnabled = null", models, StringComparison.Ordinal);
        Assert.Contains("string? KeyboardLight = null", models, StringComparison.Ordinal);
        Assert.Contains("NormalId = \"normal\"", models, StringComparison.Ordinal);
        Assert.Contains("GestureLockId = \"gesture-lock\"", models, StringComparison.Ordinal);
        Assert.Contains("SilentId = \"silent\"", models, StringComparison.Ordinal);

        Assert.DoesNotContain("CoolingProfile", models, StringComparison.Ordinal);
        Assert.DoesNotContain("PowerMode", models, StringComparison.Ordinal);
        Assert.DoesNotContain("BatteryProtection", models, StringComparison.Ordinal);
        Assert.DoesNotContain("SetCoolingProfileAsync", coordinator, StringComparison.Ordinal);
        Assert.DoesNotContain("SetPowerPreference", coordinator, StringComparison.Ordinal);
        Assert.DoesNotContain("BatteryProtection", coordinator, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveMode_IsSessionOnlyWhileCustomDefinitionsPersist()
    {
        string root = FindRepositoryRoot();
        string settings = Read(root, "src", "ThinkControl.UI", "Services", "UserSettingsService.cs");
        string coordinator = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeCoordinator.cs");

        Assert.Contains("ThinkControlModeDefinition[]? CustomModes = null", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("ActiveModeId", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("ActiveModeName", settings, StringComparison.Ordinal);
        Assert.Contains("ActiveModeId { get; private set; } = ThinkControlModeCatalog.NormalId", coordinator, StringComparison.Ordinal);
    }

    [Fact]
    public void TouchpadModeOverride_DoesNotLeakIntoOrdinaryGestureEdits()
    {
        string root = FindRepositoryRoot();
        string host = Read(root, "src", "ThinkControl.UI", "Services", "Touchpad", "TouchpadFeatureHost.cs");
        string panel = Read(root, "src", "ThinkControl.UI", "Controls", "TouchpadPanel.xaml.cs");

        Assert.Contains("bool? _transientGestureEnabled", host, StringComparison.Ordinal);
        Assert.Contains("bool releaseGestureModeOwnership = false", host, StringComparison.Ordinal);
        Assert.Contains("_transientGestureEnabled ?? persisted.Enabled", host, StringComparison.Ordinal);
        Assert.Contains("ApplyTransientGestureEnabled(bool? enabled)", host, StringComparison.Ordinal);
        Assert.Contains("releaseGestureModeOwnership: true", panel, StringComparison.Ordinal);

        int updateStart = host.IndexOf("internal void UpdateConfiguration(", StringComparison.Ordinal);
        int transientStart = host.IndexOf("internal void ApplyTransientGestureEnabled(", updateStart, StringComparison.Ordinal);
        Assert.True(updateStart >= 0 && transientStart > updateStart);
        string update = host[updateStart..transientStart];
        Assert.Contains("if (releaseGestureModeOwnership)", update, StringComparison.Ordinal);
        Assert.DoesNotContain("_app.Modes.ReleaseFacet(ThinkControlModeFacet.TouchpadGestures);\n        _app.UserSettings", update, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyboardModeOverride_UsesTransientApplyPath()
    {
        string root = FindRepositoryRoot();
        string app = Read(root, "src", "ThinkControl.UI", "App.xaml.cs");

        string apply = Slice(app,
            "internal async Task<bool> ApplyKeyboardLightModeOverrideAsync",
            "internal async Task<bool> RestoreKeyboardModeSnapshotAsync");
        Assert.DoesNotContain("UserSettings.Update", apply, StringComparison.Ordinal);

        string restore = Slice(app,
            "internal async Task<bool> RestoreKeyboardModeSnapshotAsync",
            "public void SetKeyboardBaseLevel");
        Assert.DoesNotContain("UserSettings.Update", restore, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualSubsystemChangesReleaseOnlyTheirOwnedModeFacet()
    {
        string root = FindRepositoryRoot();
        string audio = Read(root, "src", "ThinkControl.UI", "App.AudioSafety.cs");
        string keyboard = Read(root, "src", "ThinkControl.UI", "App.xaml.cs");
        string coordinator = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeCoordinator.cs");

        Assert.Contains("Modes.ReleaseFacet(ThinkControlModeFacet.AudioSafety)", audio, StringComparison.Ordinal);
        Assert.Contains("Modes.ReleaseFacet(ThinkControlModeFacet.KeyboardLight)", keyboard, StringComparison.Ordinal);
        Assert.Contains("IsModified = true", coordinator, StringComparison.Ordinal);
        Assert.DoesNotContain("ActiveModeId = ThinkControlModeCatalog.NormalId", Slice(
            coordinator,
            "internal void ReleaseFacet",
            "internal bool SaveCustomMode"), StringComparison.Ordinal);
    }

    [Fact]
    public void ResetAll_ReleasesModeAndClearsCustomDefinitions()
    {
        string root = FindRepositoryRoot();
        string reset = Read(root, "src", "ThinkControl.UI", "App.ResetDefaults.cs");

        string method = Slice(reset, "internal async Task ResetAllDefaultsAsync()", "\n}");
        Assert.Contains("await Modes.ActivateAsync(ThinkControlModeCatalog.NormalId)", method, StringComparison.Ordinal);
        Assert.Contains("CustomModes: []", method, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomModeNames_AreUniqueAndBuiltInNamesStayReserved()
    {
        string root = FindRepositoryRoot();
        string models = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeModels.cs");
        string coordinator = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeCoordinator.cs");

        Assert.Contains("existing.Name.Equals(sanitized.Name, StringComparison.OrdinalIgnoreCase)", models, StringComparison.Ordinal);
        Assert.Contains("BuiltIns.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))", models, StringComparison.Ordinal);
        Assert.Contains("existing.Name.Equals(sanitized.Name, StringComparison.OrdinalIgnoreCase)", coordinator, StringComparison.Ordinal);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find source marker: {startMarker}");
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find source marker after {startMarker}: {endMarker}");
        return source[start..end];
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine([root, .. parts])).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? current = new(start);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.UI")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tests", "ThinkControl.Core.Tests")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ThinkControl repository root for Modes validation.");
    }
}
