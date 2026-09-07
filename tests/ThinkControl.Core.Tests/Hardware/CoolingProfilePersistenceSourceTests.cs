using Xunit;

namespace ThinkControl.Core.Tests.Hardware;

public sealed class CoolingProfilePersistenceSourceTests
{
    [Fact]
    public void FirmwareProfile_IsReassertedWhenPowerBaselineOrSourceChanges()
    {
        string coordinator = ReadSource("src", "ThinkControl.Service", "LenovoCoolingPolicyCoordinator.cs");
        string method = Normalize(coordinator)
            .Split("internal bool SetBasePowerMode", StringSplitOptions.None)[1]
            .Split("internal bool SetBuiltInProfile", StringSplitOptions.None)[0];

        Assert.Contains("activeProfile = _overrideProfile", method, StringComparison.Ordinal);
        Assert.Contains("SetBuiltInProfile(activeProfile", method, StringComparison.Ordinal);
        Assert.Contains("current power source", method, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("_basePowerMode = mode", method, StringComparison.Ordinal);

        // The reassertion must reuse the reviewed semantic provider, not introduce a
        // second RPM/EC writer or a guessed current-policy read.
        Assert.DoesNotContain("SetFanPercent", method, StringComparison.Ordinal);
        Assert.DoesNotContain("SetFanLevel", method, StringComparison.Ordinal);
    }

    [Fact]
    public void UiExit_PreservesFirmwareProfileButStillReleasesDirectWriterSafetyClass()
    {
        string cooling = ReadSource("src", "ThinkControl.UI", "App.Cooling.cs");
        string initialize = Normalize(cooling)
            .Split("private void InitializeCoolingCoordinator()", StringSplitOptions.None)[1]
            .Split("private void CoolingStatusObserved", StringSplitOptions.None)[0];

        Assert.Contains("if (UsesFirmwareCoolingPolicy)", initialize, StringComparison.Ordinal);
        Assert.Contains("return;", initialize, StringComparison.Ordinal);
        Assert.Contains("HardwareClient.ReturnFanToAutoAsync", initialize, StringComparison.Ordinal);
        Assert.Contains("_coolingLifetimeCts.Cancel()", initialize, StringComparison.Ordinal);
    }

    [Fact]
    public void SavedFirmwareProfile_GetsOneBoundedStartupSettleReassert()
    {
        string cooling = ReadSource("src", "ThinkControl.UI", "App.Cooling.cs");

        Assert.Contains("CoolingStartupSettleDelay = TimeSpan.FromSeconds(7)", cooling, StringComparison.Ordinal);
        Assert.Contains("ScheduleFirmwareCoolingSettleReassert", cooling, StringComparison.Ordinal);
        Assert.Contains("ReassertFirmwareCoolingAfterStartupSettleAsync", cooling, StringComparison.Ordinal);
        Assert.Contains("Task.Delay(CoolingStartupSettleDelay", cooling, StringComparison.Ordinal);
        Assert.Contains("HardwareClient.SetThermalModeAsync(State.SelectedMode, cancellationToken)", cooling, StringComparison.Ordinal);
        Assert.Contains("HardwareClient.SetCoolingProfileAsync(definition.Name, cancellationToken)", cooling, StringComparison.Ordinal);
        Assert.Contains("one bounded retry, not a polling loop", cooling, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DispatcherTimer", cooling, StringComparison.Ordinal);
    }

    [Fact]
    public void FansSelector_UsesRuntimeStateInsteadOfPaintingSavedPreferenceAsApplied()
    {
        string panel = ReadSource("src", "ThinkControl.UI", "Controls", "FansPanel.xaml.cs");

        Assert.Contains("RuntimeProfileIdForDisplay(app.State.CoolingProfile)", panel, StringComparison.Ordinal);
        Assert.Contains("same runtime name", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CurrentProfileIdForDisplay", panel, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "app.UserSettings.Current.CoolingProfile));",
            Normalize(panel).Split("private void ApplyStatus", StringSplitOptions.None)[0],
            StringComparison.Ordinal);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string ReadSource(params string[] path)
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
                if (Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.Service")) &&
                    Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.UI")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tests", "ThinkControl.Core.Tests")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for cooling persistence validation.");
    }
}
