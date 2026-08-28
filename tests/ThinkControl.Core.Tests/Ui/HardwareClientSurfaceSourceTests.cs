using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class HardwareClientSurfaceSourceTests
{
    [Fact]
    public void CurrentUiClient_DoesNotReintroduceLegacyCoolingWrappers()
    {
        string root = FindRepositoryRoot();
        string client = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "HardwareServiceClient.cs"));
        string cooling = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.Cooling.cs"));

        Assert.DoesNotContain("SetCoolingProfileAsync", client, StringComparison.Ordinal);
        Assert.DoesNotContain("SetCustomCoolingCurveAsync", client, StringComparison.Ordinal);
        Assert.DoesNotContain("SetCustomCoolingCurveAsync", cooling, StringComparison.Ordinal);
        Assert.Contains("SetCoolingCurveAsync", client, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyServiceCompatibility_RemainsServerSideOnly()
    {
        string root = FindRepositoryRoot();
        string service = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.Service", "ServiceEngine.cs"));
        string diagnostics = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.Diagnostics.cs"));

        Assert.Contains("\"SetCoolingProfile\"", service, StringComparison.Ordinal);
        Assert.Contains("\"SetCustomCoolingCurve\"", service, StringComparison.Ordinal);
        Assert.Contains("\"SetFanPercent\" => (\"fan.percent_set\"", diagnostics, StringComparison.Ordinal);
        Assert.Contains("\"SetCoolingCurve\" => (\"fan.cooling_curve_set\"", diagnostics, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? current = new(start);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.UI")) &&
                    Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.Service")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for hardware client surface validation.");
    }
}
