using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class CompactComboDismissSourceTests
{
    [Fact]
    public void CompactSelectors_ClearPopupCaptureAndFocusAfterDismiss()
    {
        string root = FindRepositoryRoot();
        string code = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "CompactDashboard.xaml.cs"));

        foreach (string combo in new[]
        {
            "CompactPerformanceCombo",
            "CompactFanCombo",
            "CompactRefreshCombo",
            "CompactKeyboardCombo",
            "CompactModeCombo"
        })
        {
            Assert.Contains(combo, code, StringComparison.Ordinal);
        }

        Assert.Contains("combo.DropDownClosed += CompactCombo_DropDownClosed", code, StringComparison.Ordinal);
        Assert.Contains("Mouse.Capture(null)", code, StringComparison.Ordinal);
        Assert.Contains("Keyboard.ClearFocus()", code, StringComparison.Ordinal);
        Assert.Contains("Mouse.Synchronize()", code, StringComparison.Ordinal);
        Assert.Contains("combo.InvalidateVisual()", code, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectionStyles.xaml", code, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate ThinkControl repository root for Compact combo validation.");
    }
}
