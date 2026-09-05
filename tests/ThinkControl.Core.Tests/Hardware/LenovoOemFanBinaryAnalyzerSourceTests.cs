using Xunit;

namespace ThinkControl.Core.Tests.Hardware;

public sealed class LenovoOemFanBinaryAnalyzerSourceTests
{
    [Fact]
    public void Analyzer_IsStaticOnlyAndCapturesEnergyDrvWriteCallsiteEvidence()
    {
        string source = ReadSource("tools", "research", "Analyze-LenovoOemFanBinaries.ps1");

        Assert.Contains("staticOnly = $true", source, StringComparison.Ordinal);
        Assert.Contains("changeFanSpeed = [uint32]0x8310257C", source, StringComparison.Ordinal);
        Assert.Contains("queryFanSpeed = [uint32]0x83102570", source, StringComparison.Ordinal);
        Assert.Contains("legacyItsFullSpeed = [uint32]0x8310213C", source, StringComparison.Ordinal);
        Assert.Contains("Find-BytePatternOffsets", source, StringComparison.Ordinal);
        Assert.Contains("Convert-HexContext", source, StringComparison.Ordinal);
        Assert.Contains("Get-NearbyRelevantStrings", source, StringComparison.Ordinal);
        Assert.Contains("dwFanCtrlCmd", source, StringComparison.Ordinal);

        // The recovered X9 ThinkSmartSense IL contains extra thermal-policy
        // surfaces. Preserve them as static correlation clues without promoting
        // any of them into a direct fan-speed command.
        Assert.Contains("com.lenovo.its.pipe.setting", source, StringComparison.Ordinal);
        Assert.Contains("ENABLE_AC_COOL", source, StringComparison.Ordinal);
        Assert.Contains("ImprovedCoolingEfficiency", source, StringComparison.Ordinal);
        Assert.Contains("BALANCED_MODE_LCM", source, StringComparison.Ordinal);
        Assert.Contains("ThinkSmartSense/LITSSvc policy strings are research evidence only", source, StringComparison.Ordinal);

        // The analyzer may mention driver APIs in explanatory output, but it must not
        // import or execute any OEM/driver entry point. It only reads supplied files.
        Assert.DoesNotContain("[DllImport", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Start-Process", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invoke-Expression", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Add-Type", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Get-CimInstance", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionThermalPolicy_StaysOnPreviouslyReviewedModeCommands()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoThermalPolicyService.cs");

        Assert.Contains("(true, \"Quiet\") => 502u", source, StringComparison.Ordinal);
        Assert.Contains("(true, \"Balanced\") => 503u", source, StringComparison.Ordinal);
        Assert.Contains("(true, \"Performance\") => 504u", source, StringComparison.Ordinal);
        Assert.Contains("(false, \"Quiet\") => 507u", source, StringComparison.Ordinal);
        Assert.Contains("(false, \"Balanced\") => 508u", source, StringComparison.Ordinal);
        Assert.Contains("(false, \"Performance\") => 509u", source, StringComparison.Ordinal);

        // Exact-X9 IL shows 500/501 AC Cool, 505/506 DC Cool and 510/511
        // Improved Cooling Efficiency, but their runtime/product semantics have
        // not been physically correlated. They must remain research-only.
        Assert.DoesNotContain("=> 500u", source, StringComparison.Ordinal);
        Assert.DoesNotContain("=> 501u", source, StringComparison.Ordinal);
        Assert.DoesNotContain("=> 505u", source, StringComparison.Ordinal);
        Assert.DoesNotContain("=> 506u", source, StringComparison.Ordinal);
        Assert.DoesNotContain("=> 510u", source, StringComparison.Ordinal);
        Assert.DoesNotContain("=> 511u", source, StringComparison.Ordinal);
    }

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
                if (Directory.Exists(Path.Combine(current.FullName, "tools", "research")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tests", "ThinkControl.Core.Tests")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for OEM fan analyzer validation.");
    }
}
