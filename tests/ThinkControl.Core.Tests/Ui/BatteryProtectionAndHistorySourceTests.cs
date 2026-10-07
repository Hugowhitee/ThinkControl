using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class BatteryProtectionAndHistorySourceTests
{
    [Fact]
    public void BatteryPage_OffersVerifiedChargeWindowsAndUncertainWearExplanation()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.ProtectionAndHistory.cs"));
        string panel = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.xaml.cs"));

        Assert.Contains("80% (strong protection)", xaml, StringComparison.Ordinal);
        Assert.Contains("85% (recommended)", xaml, StringComparison.Ordinal);
        Assert.Contains("90% (more runtime)", xaml, StringComparison.Ordinal);
        Assert.Contains("95% (light protection)", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ChargeProtectionSwitch\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Battery preservation\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Full charge 100%", xaml, StringComparison.Ordinal);
        Assert.Contains("ChargeProtectionSwitch_Click", xaml, StringComparison.Ordinal);
        Assert.Contains("ChargeProtection_SelectionChanged", xaml, StringComparison.Ordinal);
        Assert.Contains("SetBatteryChargeThresholdsAsync(start, stop)", code, StringComparison.Ordinal);
        Assert.Contains("DisableBatteryChargeThresholdsAsync", code, StringComparison.Ordinal);

        Assert.Contains("controls:BatteryProtectionGauge", xaml, StringComparison.Ordinal);
        Assert.Contains("StartPercent=\"{Binding BatteryProtectionStartPercent}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("StopPercent=\"{Binding BatteryProtectionStopPercent}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CurrentPercent=\"{Binding BatteryPercent}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsCharging=\"{Binding BatteryCharging}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Charging stops at {stop}%", code, StringComparison.Ordinal);

        Assert.Contains("CustomChargeApply_Click", code, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CustomChargeStartComboBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CustomChargeStopComboBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Helps reduce battery wear", code, StringComparison.Ordinal);
        Assert.Contains("Estimated charging wear:", code, StringComparison.Ordinal);
        Assert.Contains("not the firmware cycle count or measured capacity loss", code, StringComparison.Ordinal);
        Assert.DoesNotContain("BatteryPreservationImpactModel.DescribeLimitWear", code, StringComparison.Ordinal);
        Assert.Contains("UpdateBatteryAgingGuidance", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("BatteryPreservationImpactModel.DescribeChargeWear", code, StringComparison.Ordinal);
        Assert.DoesNotContain("BatteryPreservationImpactModel.DescribeLimitWear", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("BatteryPreservationImpactModel.DescribeChargeWear", panel, StringComparison.Ordinal);
        Assert.Contains("RefreshChargeProtectionWearEstimate", panel, StringComparison.Ordinal);
        Assert.Contains("_batteryProtectionWriteInFlight", code, StringComparison.Ordinal);
        Assert.Contains("_lastChargeProtectionStart = 80", code, StringComparison.Ordinal);
        Assert.Contains("_lastChargeProtectionStop = 85", code, StringComparison.Ordinal);
        Assert.Contains("IsValidChargeProtectionPair(storedStart, storedStop)", code, StringComparison.Ordinal);
        Assert.Contains("stop is >= 45 and <= 95", code, StringComparison.Ordinal);
        string state = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "ViewModels", "AppState.cs"));
        Assert.Contains("public int BatteryChargeTargetPercent", state, StringComparison.Ordinal);
        Assert.Contains("BatteryEtaToChargeTarget is TimeSpan toTarget", state, StringComparison.Ordinal);
        Assert.Contains("to {target}%", state, StringComparison.Ordinal);
        Assert.Contains("Charge hold, resumes below {start}%", state, StringComparison.Ordinal);
        Assert.DoesNotContain("EstimateChargeEtaToTarget", state, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ChargeProtectionWearText\"", xaml, StringComparison.Ordinal);
        Assert.Contains("0–100%", code, StringComparison.Ordinal);

        string gauge = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "BatteryProtectionGauge.cs"));
        Assert.DoesNotContain("Tc.Warning", gauge, StringComparison.Ordinal);
        Assert.DoesNotContain("Tc.Accent", gauge, StringComparison.Ordinal);
        Assert.Contains("Tc.Success", gauge, StringComparison.Ordinal);
        Assert.Contains("DrawThreshold", gauge, StringComparison.Ordinal);
        Assert.Contains("DrawThresholdLabels", gauge, StringComparison.Ordinal);
        Assert.Contains("minimumGap = 4", gauge, StringComparison.Ordinal);
        Assert.Contains("collision symmetrically", gauge, StringComparison.Ordinal);
        Assert.Contains("current marker sits exactly on the end of the fill", gauge, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DrawLightning", gauge, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawPause", gauge, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLock", gauge, StringComparison.Ordinal);

        Assert.Contains("_batteryProtectionWritable", code, StringComparison.Ordinal);
        Assert.Contains("Custom: {selectedStart}–{selectedStop}%", code, StringComparison.Ordinal);
        Assert.Contains("{stop}% limit active", code, StringComparison.Ordinal);
        Assert.Contains("{stop}% limit (read-only)", code, StringComparison.Ordinal);
        Assert.DoesNotContain("× fewer", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BatteryHistory_IsGroupedDefaultsToSevenDaysAndDestructiveResetLivesBehindManagementSurface()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.ProtectionAndHistory.cs"));
        string panel = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.xaml.cs"));
        string service = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "BatteryHistoryService.cs"));

        Assert.Contains("Days stay compact", xaml, StringComparison.Ordinal);
        Assert.Contains("<Expander Header=\"Manage history\"", xaml, StringComparison.Ordinal);
        Assert.Contains("7 days", xaml, StringComparison.Ordinal);
        Assert.Contains("14 days", xaml, StringComparison.Ordinal);
        Assert.Contains("30 days", xaml, StringComparison.Ordinal);
        Assert.Contains("Reset all history…", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Clear history\"", xaml, StringComparison.Ordinal);
        Assert.Contains("_historyVisibleDays = 7", code, StringComparison.Ordinal);
        Assert.Contains("HistoryRange_Click", code, StringComparison.Ordinal);
        Assert.Contains("Take(_historyVisibleDays)", panel, StringComparison.Ordinal);
        Assert.Contains("learned charge/discharge estimates", code, StringComparison.Ordinal);
        Assert.Contains("MessageBoxImage.Warning", code, StringComparison.Ordinal);
        Assert.Contains("ConfigureDetailedRetentionDays(days)", code, StringComparison.Ordinal);
        Assert.Contains("SummaryRetentionDays = 365", File.ReadAllText(Path.Combine(root, "src", "ThinkControl.Core", "Battery", "BatteryHistoryRetentionPolicy.cs")), StringComparison.Ordinal);
        Assert.Contains("GetRecentDays", service, StringComparison.Ordinal);
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
        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for battery UI validation.");
    }
}
