using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class BatteryEtaTargetSourceTests
{
    [Fact]
    public void RuntimeEta_UsesVerifiedPreservationStopThreshold()
    {
        string runtime = Read("src", "ThinkControl.UI", "App.RuntimeRefresh.cs");
        string diagnostics = Read("src", "ThinkControl.UI", "App.Diagnostics.cs");

        Assert.Contains("ResolveBatteryChargeTargetPercent()", runtime, StringComparison.Ordinal);
        Assert.Contains("State.BatteryProtectionStopPercent is int stop", runtime, StringComparison.Ordinal);
        Assert.Contains("ResolveBatteryChargeTargetPercent()));", runtime, StringComparison.Ordinal);
        Assert.Contains("State.BatteryEtaToChargeTarget = eta.ToChargeTarget", runtime, StringComparison.Ordinal);

        Assert.Contains("previousChargeTarget = ResolveBatteryChargeTargetPercent()", diagnostics, StringComparison.Ordinal);
        Assert.Contains("nextChargeTarget = ResolveBatteryChargeTargetPercent()", diagnostics, StringComparison.Ordinal);
        Assert.Contains("_runtimeBatteryEta.Reset()", diagnostics, StringComparison.Ordinal);
        Assert.Contains("State.BatteryEtaToChargeTarget = null", diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public void VisibleEta_CopyNamesActualTargetAndReachedLimit()
    {
        string state = Read("src", "ThinkControl.UI", "ViewModels", "AppState.cs");

        Assert.Contains("public int BatteryChargeTargetPercent", state, StringComparison.Ordinal);
        Assert.Contains("$\"~{FormatDuration(toTarget)} to {target}%\"", state, StringComparison.Ordinal);
        Assert.Contains("$\"Estimating to {target}%…\"", state, StringComparison.Ordinal);
        Assert.Contains("$\"Almost at {target}%\"", state, StringComparison.Ordinal);
        Assert.Contains("$\"Charge limit {stop}%\"", state, StringComparison.Ordinal);
        Assert.Contains("$\"Charge hold, resumes below {start}%\"", state, StringComparison.Ordinal);
        Assert.Contains("\"Fully charged\"", state, StringComparison.Ordinal);
        Assert.Contains("\"~{FormatDuration(toTarget)} to full\"", state, StringComparison.Ordinal);
        Assert.DoesNotContain("BatteryEtaToFull", state, StringComparison.Ordinal);
    }

    [Fact]
    public void FullBatteryTelemetry_DoesNotKeepASeparateToFullSemantic()
    {
        string service = Read("src", "ThinkControl.UI", "Services", "BatteryTelemetryService.cs");
        string app = Read("src", "ThinkControl.UI", "App.xaml.cs");

        Assert.Contains("EstimatedTimeToChargeTarget", service, StringComparison.Ordinal);
        Assert.Contains("public BatteryTelemetrySnapshot Read(int chargeTargetPercent)", service, StringComparison.Ordinal);
        Assert.Contains("_cachedChargeTargetPercent == targetPercent", service, StringComparison.Ordinal);
        Assert.Contains("targetWh = raw.FullChargeCapacityWh.Value * targetPercent / 100d", service, StringComparison.Ordinal);
        Assert.Contains("raw.Percent is int percent && percent >= targetPercent", service, StringComparison.Ordinal);
        Assert.DoesNotContain("EstimatedTimeToFull", service, StringComparison.Ordinal);

        Assert.Contains("BatteryTelemetryService.Read(ResolveBatteryChargeTargetPercent())", app, StringComparison.Ordinal);
        Assert.Contains("battery.EstimatedTimeToChargeTarget", app, StringComparison.Ordinal);
    }

    [Fact]
    public void AllPrimaryBatterySurfaces_UseOneSharedEtaText()
    {
        string compact = Read("src", "ThinkControl.UI", "Controls", "CompactDashboard.Metrics.cs");
        string home = Read("src", "ThinkControl.UI", "AdvancedWindow.HomeDashboard.cs");
        string battery = Read("src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.xaml");

        Assert.Contains("\"BatteryEtaText\"", compact, StringComparison.Ordinal);
        Assert.Contains("new Binding(\"BatteryEtaText\")", home, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding BatteryEtaText}\"", battery, StringComparison.Ordinal);
    }

    private static string Read(params string[] path)
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine([root, .. path]));
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

        throw new DirectoryNotFoundException("Could not locate ThinkControl repository root for battery ETA target validation.");
    }
}
