using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class Alpha47UxSourceTests
{
    [Fact]
    public void UpdateAttention_FirstVisibleWindowOffersInstallNowOrLater()
    {
        string root = FindRepositoryRoot();
        string attention = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.AttentionNotifications.cs"));
        string updateUi = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.UpdateUi.cs"));

        Assert.Contains("ready ? \"Install now\" : \"Open Updates\"", attention, StringComparison.Ordinal);
        Assert.Contains("dismissText: \"Later\"", attention, StringComparison.Ordinal);
        Assert.Contains("autoHide: false", attention, StringComparison.Ordinal);
        string toast = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "AttentionToastService.cs"));
        Assert.Contains("if (_autoHidePresentation)", toast, StringComparison.Ordinal);

        string visibility = attention.Split("private bool CanShowAttentionNow()", StringSplitOptions.None)[1]
            .Split("private static bool NeedsProactiveHardwareAttention", StringSplitOptions.None)[0];
        Assert.DoesNotContain("IsTrayOnlyLaunch()", visibility, StringComparison.Ordinal);
        Assert.Contains("CompactWindow?.IsVisible == true", visibility, StringComparison.Ordinal);
        Assert.Contains("_advancedWindow?.IsVisible == true", visibility, StringComparison.Ordinal);

        string homeClick = updateUi.Split("private async void HomeUpdateCheck_Click", StringSplitOptions.None)[1]
            .Split("private void RefreshHomeUpdateUi", StringSplitOptions.None)[0];
        Assert.Contains("_app.LatestUpdateResult is { Available: true }", homeClick, StringComparison.Ordinal);
        Assert.Contains("Navigate(\"Updates\")", homeClick, StringComparison.Ordinal);
        Assert.Contains("\"Install update  ›\"", updateUi, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomaticUpdates_RecheckOnlyWhenRuntimeStateIsStale()
    {
        string root = FindRepositoryRoot();
        string updates = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.AutoUpdates.cs"));
        string runtime = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.RuntimeRefresh.cs"));

        Assert.Contains("AutomaticUpdateCheckStaleAfter = TimeSpan.FromHours(4)", updates, StringComparison.Ordinal);
        Assert.Contains("UpdateCheckHistoryService.Read()", updates, StringComparison.Ordinal);
        Assert.Contains("RequestAutomaticUpdateCheckIfStale", updates, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherTimer", updates, StringComparison.Ordinal);

        string activation = runtime.Split("private void Runtime_Activated", StringSplitOptions.None)[1]
            .Split("private void Runtime_Exit", StringSplitOptions.None)[0];
        Assert.Contains("RequestAutomaticUpdateCheckIfStale();", activation, StringComparison.Ordinal);

        string resume = runtime.Split("if (e.Mode == PowerModes.Resume)", StringSplitOptions.None)[1]
            .Split("private void Runtime_Activated", StringSplitOptions.None)[0];
        Assert.Contains("RequestAutomaticUpdateCheckIfStale();", resume, StringComparison.Ordinal);
    }

    [Fact]
    public void AdvancedHome_PowerProfilesCoverBatteryAndPluggedIn()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.HomeQuickControls.cs"));

        Assert.Contains("x:Name=\"HomeQuiet\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"Battery:Quiet\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HomeAcQuiet\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"Ac:Quiet\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HomeAcPerformance\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"HomePowerMode_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("_app.GetPowerPreference(onBattery: true)", code, StringComparison.Ordinal);
        Assert.Contains("_app.GetPowerPreference(onBattery: false)", code, StringComparison.Ordinal);
        Assert.Contains("_app.SetPowerPreference(mode, onBattery)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void AdvancedHome_FanAutoAndMoreProfilesAreRealControls()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.HomeQuickControls.cs"));

        Assert.Contains("x:Name=\"HomeFanAutoSwitch\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HomeFanMoreButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"HomeFanAuto_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"HomeFanMore_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("new ContextMenu", code, StringComparison.Ordinal);
        Assert.Contains("HomeFanAutoSwitch.IsChecked == true ? \"Auto\" : \"Balanced\"", code, StringComparison.Ordinal);
        string autoClick = code.Split("private async void HomeFanAuto_Click", StringSplitOptions.None)[1]
            .Split("private async void HomeFanQuick_Click", StringSplitOptions.None)[0];
        Assert.Contains("if (_homeFanBusy)", autoClick, StringComparison.Ordinal);
        Assert.DoesNotContain("if (_syncing || _homeFanBusy)", autoClick, StringComparison.Ordinal);
        Assert.Contains("HomeFanQuickGrid.IsEnabled = enabled && !autoActive", code, StringComparison.Ordinal);
        Assert.Contains("HomeFanMoreButton.IsEnabled = enabled && !autoActive", code, StringComparison.Ordinal);
        Assert.Contains("HomeFanMoreButton.Opacity = HomeFanMoreButton.IsEnabled ? 1.0 : 0.42", code, StringComparison.Ordinal);
        Assert.DoesNotContain("MoreFanProfilesLabel", code, StringComparison.Ordinal);
        Assert.DoesNotContain("HomeFanProfileCombo", code, StringComparison.Ordinal);
    }

    [Fact]
    public void AdvancedHome_SensorsMetricOpensExistingSensorDetails()
    {
        string root = FindRepositoryRoot();
        string home = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.HomeDashboard.cs"));
        string normalized = home.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("openSensorDetails: true", home, StringComparison.Ordinal);
        Assert.Contains("app.OpenSensorDetails(this);", home, StringComparison.Ordinal);
        Assert.Contains("else\n                Navigate(page);", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void AudioSafety_LivesOnHomeAndMediaControls_NotSettings()
    {
        string root = FindRepositoryRoot();
        string compactXaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "CompactDashboard.xaml"));
        string compactCode = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "CompactDashboard.QuickControls.cs"));
        string advancedXaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.xaml"));
        string preferences = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.AppPreferences.cs"));

        Assert.Contains("Text=\"Audio safety\"", compactXaml, StringComparison.Ordinal);
        Assert.Contains("\"Normal\", \"Gesture lock\", \"Silent\"", compactCode, StringComparison.Ordinal);
        Assert.Contains("keyboard and Windows/app audio still work", compactXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HomeAudioSafetyNormal\"", advancedXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HomeAudioSafetyMediaLock\"", advancedXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HomeAudioSafetySilent\"", advancedXaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Audio safety\"", advancedXaml, StringComparison.Ordinal);

        Assert.DoesNotContain("CreateAudioSafetyCard", preferences, StringComparison.Ordinal);
        Assert.DoesNotContain("ThinkControl.Settings.AudioSafety", preferences, StringComparison.Ordinal);
    }

    [Fact]
    public void BatteryPreservation_HasLiveStateAndTransitionFeedback()
    {
        string root = FindRepositoryRoot();
        string state = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "ViewModels", "AppState.cs"));
        string attention = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.BatteryProtectionAttention.cs"));
        string panel = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.ProtectionAndHistory.cs"));
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.xaml"));

        Assert.Contains("BatteryProtectionSummaryText", state, StringComparison.Ordinal);
        Assert.Contains("Charging paused near", state, StringComparison.Ordinal);
        Assert.Contains("Battery preservation paused charging", attention, StringComparison.Ordinal);
        Assert.Contains("Battery preservation resumed charging", attention, StringComparison.Ordinal);
        Assert.Contains("ShowBatteryPreservationApplied", panel, StringComparison.Ordinal);
        Assert.Contains("ShowBatteryPreservationDisabled", panel, StringComparison.Ordinal);
        Assert.Contains("Text=\"Battery preservation\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BatteryProtectionBehaviorText", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Set limit\" Style=\"{StaticResource TcButton}\" IsEnabled=\"False\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyboardEffects_ExperimentalFallbackRequiresExplicitSessionOptIn()
    {
        string root = FindRepositoryRoot();
        string state = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "ViewModels", "AppState.cs"));
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "KeyboardEffectsPanel.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "KeyboardEffectsPanel.xaml.cs"));
        string service = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "KeyboardEffectService.cs"));

        Assert.Contains("ExperimentalKeyboardEffectsEnabled", state, StringComparison.Ordinal);
        Assert.Contains("KeyboardEffectsUsable", state, StringComparison.Ordinal);
        Assert.Contains("Text=\"EXPERIMENTAL\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ExperimentalEffectsSwitch\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MessageBoxButton.YesNo", code, StringComparison.Ordinal);
        Assert.Contains("for this ThinkControl session", code, StringComparison.Ordinal);
        Assert.Contains("_state.KeyboardEffectsUsable", service, StringComparison.Ordinal);
        Assert.Contains("MinHardwareWriteInterval = TimeSpan.FromMilliseconds(260)", service, StringComparison.Ordinal);
    }

    [Fact]
    public void BatteryHealthTrend_SamplesCapacityWithoutRequiringAChargeSession()
    {
        string root = FindRepositoryRoot();
        string history = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "BatteryHistoryService.cs"));
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.xaml"));

        string record = history.Split("public BatteryHistoryView Record(", StringSplitOptions.None)[1]
            .Split("public BatteryHistoryView GetView()", StringSplitOptions.None)[0];
        int sampleIndex = record.IndexOf("RecordHealthSample(now, fullChargeWh, designWh)", StringComparison.Ordinal);
        int chargeBranchIndex = record.IndexOf("if (charging)", StringComparison.Ordinal);
        Assert.True(sampleIndex >= 0 && chargeBranchIndex > sampleIndex);

        Assert.Contains("List<BatteryHealthSample> HealthSamples", history, StringComparison.Ordinal);
        Assert.Contains("SchemaVersion { get; set; } = 5", history, StringComparison.Ordinal);
        string runtime = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.RuntimeRefresh.cs"));
        string app = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.xaml.cs"));
        Assert.Contains("_runtimeBatteryDesignWh", runtime, StringComparison.Ordinal);
        Assert.Contains("battery.DesignCapacityWh is > 0", app, StringComparison.Ordinal);
        Assert.Contains("A charge cap such as 80–90% does not need to be disabled", xaml, StringComparison.Ordinal);
    }

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

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for alpha.47 UX validation.");
    }
}
