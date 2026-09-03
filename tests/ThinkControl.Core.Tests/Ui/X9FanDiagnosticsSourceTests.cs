using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class X9FanDiagnosticsSourceTests
{
    [Fact]
    public void ExistingStatusObservation_FeedsBoundedX9FanSamples()
    {
        string root = FindRepositoryRoot();
        string appDiagnostics = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.Diagnostics.cs"));
        string fanDiagnostics = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.FanDiagnostics.cs"));
        string contracts = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.Core", "Diagnostics", "DiagnosticsContracts.cs"));

        Assert.Contains("RecordFanTelemetrySample(telemetry);", appDiagnostics, StringComparison.Ordinal);
        Assert.Contains("FanDiagnosticSampleInterval = TimeSpan.FromSeconds(30)", fanDiagnostics, StringComparison.Ordinal);
        Assert.Contains("FanDiagnosticRpmBucket = 250", fanDiagnostics, StringComparison.Ordinal);
        Assert.Contains("State.MachineType, \"21Q6\"", fanDiagnostics, StringComparison.Ordinal);
        Assert.Contains("State.MachineType, \"21Q7\"", fanDiagnostics, StringComparison.Ordinal);
        Assert.Contains("fan.telemetry_sample", fanDiagnostics, StringComparison.Ordinal);
        Assert.Contains("[\"fan1RpmBucket\"]", fanDiagnostics, StringComparison.Ordinal);
        Assert.Contains("[\"fan2RpmBucket\"]", fanDiagnostics, StringComparison.Ordinal);
        Assert.Contains("FanRpm: fans.FirstOrDefault()?.Rpm", fanDiagnostics, StringComparison.Ordinal);
        Assert.Contains("\"fan1Rpm\"", contracts, StringComparison.Ordinal);
        Assert.Contains("\"fan2Rpm\"", contracts, StringComparison.Ordinal);

        // Diagnostics consume the already-observed service response. They must not add
        // another polling loop or direct hardware request just for reporting.
        Assert.DoesNotContain("GetStatusAsync", fanDiagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherTimer", fanDiagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", fanDiagnostics, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for fan diagnostics validation.");
    }
}
