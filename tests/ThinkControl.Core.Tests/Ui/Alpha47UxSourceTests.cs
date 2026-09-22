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
        Assert.DoesNotContain("MoreFanProfilesLabel", code, StringComparison.Ordinal);
        Assert.DoesNotContain("HomeFanProfileCombo", code, StringComparison.Ordinal);
    }

    [Fact]
    public void AudioSafety_IsNamedInCompactAndVisibleOnAdvancedHome()
    {
        string root = FindRepositoryRoot();
        string compactXaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "CompactDashboard.xaml"));
        string compactCode = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "CompactDashboard.QuickControls.cs"));
        string advancedXaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.xaml"));

        Assert.Contains("Width=\"138\"", compactXaml, StringComparison.Ordinal);
        Assert.Contains("Audio safety: blocks accidental ThinkControl volume/media actions", compactXaml, StringComparison.Ordinal);
        Assert.Contains("\"Audio · Normal\"", compactCode, StringComparison.Ordinal);
        Assert.Contains("\"Audio · Media lock\"", compactCode, StringComparison.Ordinal);
        Assert.Contains("\"Audio · Silent\"", compactCode, StringComparison.Ordinal);

        Assert.Contains("x:Name=\"HomeAudioSafetyNormal\"", advancedXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HomeAudioSafetyMediaLock\"", advancedXaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HomeAudioSafetySilent\"", advancedXaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Audio safety\"", advancedXaml, StringComparison.Ordinal);
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
