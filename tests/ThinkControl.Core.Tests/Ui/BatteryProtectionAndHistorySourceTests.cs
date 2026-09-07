using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class BatteryProtectionAndHistorySourceTests
{
    [Fact]
    public void BatteryPage_OffersSimpleVerifiedChargeCareChoicesWithoutInventedCycleClaims()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.ProtectionAndHistory.cs"));

        Assert.Contains("Battery care · 80% (recommended)", xaml, StringComparison.Ordinal);
        Assert.Contains("Full charge · 100%", xaml, StringComparison.Ordinal);
        Assert.Contains("ChargeProtection_SelectionChanged", xaml, StringComparison.Ordinal);
        Assert.Contains("20 percentage points of headroom from full charge", code, StringComparison.Ordinal);
        Assert.Contains("SetBatteryChargeLimitAsync(percent)", code, StringComparison.Ordinal);
        Assert.Contains("_batteryProtectionWritable", code, StringComparison.Ordinal);
        Assert.DoesNotContain("fewer cycles", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fewer cycles", code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BatteryHistory_IsGroupedAndDestructiveResetLivesBehindManagementSurface()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.ProtectionAndHistory.cs"));
        string service = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "BatteryHistoryService.cs"));

        Assert.Contains("Days stay compact", xaml, StringComparison.Ordinal);
        Assert.Contains("<Expander Header=\"Manage history\"", xaml, StringComparison.Ordinal);
        Assert.Contains("7 days", xaml, StringComparison.Ordinal);
        Assert.Contains("14 days", xaml, StringComparison.Ordinal);
        Assert.Contains("30 days", xaml, StringComparison.Ordinal);
        Assert.Contains("Reset all history…", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Clear history\"", xaml, StringComparison.Ordinal);
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
