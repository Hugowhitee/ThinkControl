using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class CapabilityDrivenUiCopySourceTests
{
    [Fact]
    public void GenericKeyboardAutoCopy_IsProviderNeutral()
    {
        string root = FindRepositoryRoot();
        string keyboard = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.Keyboard.cs"));
        string state = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "ViewModels", "AppState.cs"));

        Assert.Contains("active provider's verified firmware-managed mode", keyboard, StringComparison.Ordinal);
        Assert.Contains("Firmware Auto · provider managed", keyboard, StringComparison.Ordinal);
        Assert.Contains("provider that advertises safe repeated backlight writes", keyboard, StringComparison.Ordinal);
        Assert.DoesNotContain("Auto is Lenovo's native firmware mode", keyboard, StringComparison.Ordinal);
        Assert.DoesNotContain("Lenovo Auto · firmware managed", keyboard, StringComparison.Ordinal);
        Assert.Contains("public bool CanKeyboardEffects", state, StringComparison.Ordinal);
    }

    [Fact]
    public void CalibrationRequiredSnapshot_ReportsFirmwareAutoAsTheAppliedState()
    {
        string root = FindRepositoryRoot();
        string snapshot = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "FansPanel.ManualTestSnapshot.cs"));

        Assert.Contains("_app.State.FanStateText = \"Firmware Auto\";", snapshot, StringComparison.Ordinal);
        Assert.Contains("AppliedLevelText.Text = _app.State.FanStateText;", snapshot, StringComparison.Ordinal);
        Assert.Contains("PrepareCalibrationRequiredForSnapshot", snapshot, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for capability-driven UI validation.");
    }
}
