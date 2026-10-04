using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class ThinkControlModesSourceTests
{
    [Fact]
    public void Modes_AreSparseContextOverlays_NotASecondSettingsStore()
    {
        string root = FindRepositoryRoot();
        string models = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeModels.cs");
        string coordinator = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeCoordinator.cs");

        Assert.Contains("string? PerformanceMode = null", models, StringComparison.Ordinal);
        Assert.Contains("string? CoolingProfile = null", models, StringComparison.Ordinal);
        Assert.Contains("string? RefreshRate = null", models, StringComparison.Ordinal);
        Assert.Contains("string? AudioSafety = null", models, StringComparison.Ordinal);
        Assert.Contains("bool? TouchpadGesturesEnabled = null", models, StringComparison.Ordinal);
        Assert.Contains("string? KeyboardLight = null", models, StringComparison.Ordinal);

        Assert.Contains("ThinkControlModeFacet.PerformanceMode", coordinator, StringComparison.Ordinal);
        Assert.Contains("ThinkControlModeFacet.CoolingProfile", coordinator, StringComparison.Ordinal);
        Assert.Contains("ThinkControlModeFacet.RefreshRate", coordinator, StringComparison.Ordinal);
        Assert.Contains("ThinkControlModeFacet.AudioSafety", coordinator, StringComparison.Ordinal);
        Assert.Contains("ThinkControlModeFacet.TouchpadGestures", coordinator, StringComparison.Ordinal);
        Assert.Contains("ThinkControlModeFacet.KeyboardLight", coordinator, StringComparison.Ordinal);

        Assert.DoesNotContain("BatteryProtection", models, StringComparison.Ordinal);
        Assert.DoesNotContain("Brightness", models, StringComparison.Ordinal);
        Assert.DoesNotContain("Microphone", models, StringComparison.Ordinal);
        Assert.DoesNotContain("Theme", models, StringComparison.Ordinal);
        Assert.DoesNotContain("ExperimentalKeyboardEffects", models, StringComparison.Ordinal);
    }

    [Fact]
    public void VisibleModes_AreNoModePlusUserModes_NotLegacyAudioSafetyBuiltIns()
    {
        string root = FindRepositoryRoot();
        string models = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeModels.cs");
        string panel = Read(root, "src", "ThinkControl.UI", "Controls", "ModesPanel.xaml.cs");
        string xaml = Read(root, "src", "ThinkControl.UI", "Controls", "ModesPanel.xaml");

        Assert.Contains("new(NormalId, \"No mode\")", models, StringComparison.Ordinal);
        Assert.Contains("VisibleModes", models, StringComparison.Ordinal);
        Assert.Contains("[NoMode, .. (customs ?? [])]", models, StringComparison.Ordinal);
        Assert.Contains("LegacyBuiltIns", models, StringComparison.Ordinal);
        Assert.Contains("StarterModes", models, StringComparison.Ordinal);
        Assert.Contains("\"custom:focus\"", models, StringComparison.Ordinal);
        Assert.Contains("\"custom:battery-saver\"", models, StringComparison.Ordinal);
        Assert.Contains("\"custom:performance\"", models, StringComparison.Ordinal);
        Assert.Contains("ModeRows.Children.Add(CreateModeRow(mode))", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInRows", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomRows", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Saved modes\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("StarterModesPanel", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveMode_IsSessionOnlyWhileDefinitionsAndTriggersPersist()
    {
        string root = FindRepositoryRoot();
        string settings = Read(root, "src", "ThinkControl.UI", "Services", "UserSettingsService.cs");
        string coordinator = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeCoordinator.cs");
        string models = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeModels.cs");

        Assert.Contains("ThinkControlModeDefinition[]? CustomModes = null", settings, StringComparison.Ordinal);
        Assert.Contains("bool StarterModesSeeded = false", settings, StringComparison.Ordinal);
        Assert.Contains("ThinkControlModeCatalog.SeedStarterModes", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("ActiveModeId", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("ActiveModeName", settings, StringComparison.Ordinal);
        Assert.Contains("ActiveModeId { get; private set; } = ThinkControlModeCatalog.NormalId", coordinator, StringComparison.Ordinal);
        Assert.Contains("ThinkControlModeTrigger[]? Triggers = null", models, StringComparison.Ordinal);
        Assert.Contains("bool AutomationEnabled = false", models, StringComparison.Ordinal);
    }

    [Fact]
    public void StarterModes_AreSeededOnceAsNormalEditableUserModes()
    {
        string root = FindRepositoryRoot();
        string models = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeModels.cs");
        string settings = Read(root, "src", "ThinkControl.UI", "Services", "UserSettingsService.cs");

        Assert.Contains("SeedStarterModes", models, StringComparison.Ordinal);
        Assert.Contains("CreateStarterTemplate", models, StringComparison.Ordinal);
        Assert.Contains("seedStarterModes = !loaded.StarterModesSeeded", settings, StringComparison.Ordinal);
        Assert.Contains("StarterModesSeeded = true", settings, StringComparison.Ordinal);
        Assert.Contains("SaveInternal(_current)", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void Automation_RunsInUserSessionAndUsesDeterministicContextPriority()
    {
        string root = FindRepositoryRoot();
        string automation = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeAutomationService.cs");
        string appModes = Read(root, "src", "ThinkControl.UI", "App.Modes.cs");

        Assert.Contains("DispatcherTimer", automation, StringComparison.Ordinal);
        Assert.Contains("ModeTriggerEnvironment.Capture", automation, StringComparison.Ordinal);
        Assert.Contains("\"Process\" => 500", automation, StringComparison.Ordinal);
        Assert.Contains("\"Wifi\" => 400", automation, StringComparison.Ordinal);
        Assert.Contains("\"BatteryBelow\" => 320", automation, StringComparison.Ordinal);
        Assert.Contains("\"Power\" => 300", automation, StringComparison.Ordinal);
        Assert.Contains("\"Schedule\" => 200", automation, StringComparison.Ordinal);
        Assert.Contains("SuppressUntilContextChanges", automation, StringComparison.Ordinal);
        Assert.Contains("NotifyManualModeSelection", appModes, StringComparison.Ordinal);
        Assert.DoesNotContain("ThinkControl.Service", automation, StringComparison.Ordinal);
    }

    [Fact]
    public void PerformanceCoolingAndRefreshModeWrites_DoNotPersistOrdinaryPreferences()
    {
        string root = FindRepositoryRoot();
        string power = Read(root, "src", "ThinkControl.UI", "App.PowerProfiles.cs");
        string cooling = Read(root, "src", "ThinkControl.UI", "App.Cooling.cs");
        string app = Read(root, "src", "ThinkControl.UI", "App.xaml.cs");

        string powerOverride = Slice(power, "internal bool ApplyPowerModeOverride", "internal bool RestorePowerModeOverride");
        Assert.DoesNotContain("UserSettings.Update", powerOverride, StringComparison.Ordinal);
        Assert.Contains("PowerModeService.SetEffective", powerOverride, StringComparison.Ordinal);

        string coolingOverride = Slice(cooling, "internal Task<bool> ApplyCoolingModeOverrideAsync", "private async Task<bool> ApplyCoolingProfileAsync");
        Assert.DoesNotContain("UserSettings.Update", coolingOverride, StringComparison.Ordinal);
        Assert.Contains("persistSelection: false", coolingOverride, StringComparison.Ordinal);

        string refreshApply = Slice(app, "internal bool ApplyRefreshModeOverride", "internal bool RestoreRefreshModeSnapshot");
        Assert.DoesNotContain("UserSettings.Update", refreshApply, StringComparison.Ordinal);
        Assert.Contains("DisplayService.SetRefreshRate", refreshApply, StringComparison.Ordinal);
    }

    [Fact]
    public void TouchpadAndKeyboardTransientPaths_RemainNonPersistent()
    {
        string root = FindRepositoryRoot();
        string host = Read(root, "src", "ThinkControl.UI", "Services", "Touchpad", "TouchpadFeatureHost.cs");
        string panel = Read(root, "src", "ThinkControl.UI", "Controls", "TouchpadPanel.xaml.cs");
        string app = Read(root, "src", "ThinkControl.UI", "App.xaml.cs");

        Assert.Contains("bool? _transientGestureEnabled", host, StringComparison.Ordinal);
        Assert.Contains("bool releaseGestureModeOwnership = false", host, StringComparison.Ordinal);
        Assert.Contains("_transientGestureEnabled ?? persisted.Enabled", host, StringComparison.Ordinal);
        Assert.Contains("ApplyTransientGestureEnabled(bool? enabled)", host, StringComparison.Ordinal);
        Assert.Contains("releaseGestureModeOwnership: true", panel, StringComparison.Ordinal);

        string keyboardApply = Slice(app,
            "internal async Task<bool> ApplyKeyboardLightModeOverrideAsync",
            "internal async Task<bool> RestoreKeyboardModeSnapshotAsync");
        Assert.DoesNotContain("UserSettings.Update", keyboardApply, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualSubsystemChanges_ReleaseOnlyTheirOwnedFacet()
    {
        string root = FindRepositoryRoot();
        string audio = Read(root, "src", "ThinkControl.UI", "App.AudioSafety.cs");
        string keyboard = Read(root, "src", "ThinkControl.UI", "App.xaml.cs");
        string power = Read(root, "src", "ThinkControl.UI", "App.PowerProfiles.cs");
        string cooling = Read(root, "src", "ThinkControl.UI", "App.Cooling.cs");
        string coordinator = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeCoordinator.cs");

        Assert.Contains("Modes.ReleaseFacet(ThinkControlModeFacet.AudioSafety)", audio, StringComparison.Ordinal);
        Assert.Contains("Modes.ReleaseFacet(ThinkControlModeFacet.KeyboardLight)", keyboard, StringComparison.Ordinal);
        Assert.Contains("Modes.ReleaseFacet(ThinkControlModeFacet.PerformanceMode)", power, StringComparison.Ordinal);
        Assert.Contains("Modes.ReleaseFacet(ThinkControlModeFacet.CoolingProfile)", cooling, StringComparison.Ordinal);
        Assert.Contains("IsModified = true", coordinator, StringComparison.Ordinal);
    }

    [Fact]
    public void ModeTransition_PublishesPendingTargetBeforeSlowSubsystemWrites()
    {
        string root = FindRepositoryRoot();
        string coordinator = Read(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeCoordinator.cs");
        string modesPanel = Read(root, "src", "ThinkControl.UI", "Controls", "ModesPanel.xaml.cs");
        string home = Read(root, "src", "ThinkControl.UI", "AdvancedWindow.HomeQuickControls.cs");
        string compact = Read(root, "src", "ThinkControl.UI", "Controls", "CompactDashboard.QuickControls.cs");

        int pending = coordinator.IndexOf("TransitionModeId = target.Id;", StringComparison.Ordinal);
        int firstApply = coordinator.IndexOf("ApplyFacetAsync(target, facet)", StringComparison.Ordinal);
        Assert.True(pending >= 0 && firstApply > pending);
        Assert.Contains("internal string VisibleModeId => TransitionModeId ?? ActiveModeId", coordinator, StringComparison.Ordinal);
        Assert.Contains("Applying…", modesPanel, StringComparison.Ordinal);
        Assert.Contains("_app.Modes.VisibleModeId", home, StringComparison.Ordinal);
        Assert.Contains("_app.Modes.VisibleModeId", compact, StringComparison.Ordinal);
    }

    [Fact]
    public void ModesEditor_ExposesSettingsAndAutomationProgressively()
    {
        string root = FindRepositoryRoot();
        string xaml = Read(root, "src", "ThinkControl.UI", "Controls", "ModesPanel.xaml");
        string code = Read(root, "src", "ThinkControl.UI", "Controls", "ModesPanel.xaml.cs");

        Assert.Contains("Text=\"Controls\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Add setting\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Automation\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Add trigger\"", xaml, StringComparison.Ordinal);
        Assert.Contains("\"Wi-Fi network\", \"Wifi\"", code, StringComparison.Ordinal);
        Assert.Contains("\"App running\", \"Process\"", code, StringComparison.Ordinal);
        Assert.Contains("\"Power source\", \"Power\"", code, StringComparison.Ordinal);
        Assert.Contains("\"Battery level\", \"BatteryBelow\"", code, StringComparison.Ordinal);
        Assert.Contains("\"Schedule\", \"Schedule\"", code, StringComparison.Ordinal);
        Assert.DoesNotContain("No controls yet", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ModeSelector\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"New mode ▾\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Click=\"StarterMode_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header = \"From template\"", code, StringComparison.Ordinal);
        Assert.Contains("ThinkControlModeCatalog.CreateStarterTemplate", code, StringComparison.Ordinal);
        Assert.Contains("ModeSelector_SelectionChanged", code, StringComparison.Ordinal);
        Assert.Contains("LastTransitionError", code, StringComparison.Ordinal);
        Assert.Contains("Save & apply", code, StringComparison.Ordinal);
        Assert.DoesNotContain("select.Click += Activate_Click", code, StringComparison.Ordinal);
        Assert.DoesNotContain("MutedText(\"Select\")", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Content = mode.Id == ThinkControlModeCatalog.NormalId ? \"Use\" : \"Activate\"", code, StringComparison.Ordinal);
        Assert.Contains("InlineButton(\"×\", RemoveSetting_Click", code, StringComparison.Ordinal);
        Assert.Contains("InlineButton(\"×\", RemoveTrigger_Click", code, StringComparison.Ordinal);
        string editorSeparator = Slice(code, "private static Border SeparatorRow(UIElement child)", "\n    private async void Reapply_Click");
        Assert.Contains("BorderThickness = new Thickness(0)", editorSeparator, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderThickness = new Thickness(0, 1, 0, 0)", editorSeparator, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetAll_ReleasesModeAndClearsDefinitions()
    {
        string root = FindRepositoryRoot();
        string reset = Read(root, "src", "ThinkControl.UI", "App.ResetDefaults.cs");

        string method = Slice(reset, "internal async Task ResetAllDefaultsAsync()", "\n}");
        Assert.Contains("await Modes.ActivateAsync(ThinkControlModeCatalog.NormalId)", method, StringComparison.Ordinal);
        Assert.Contains("ThinkControlModeCatalog.StarterModes", method, StringComparison.Ordinal);
        Assert.Contains("StarterModesSeeded: true", method, StringComparison.Ordinal);
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
