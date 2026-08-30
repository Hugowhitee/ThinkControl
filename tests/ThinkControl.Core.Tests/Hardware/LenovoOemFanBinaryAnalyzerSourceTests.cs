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

        // The analyzer may mention driver APIs in explanatory output, but it must not
        // import or execute any OEM/driver entry point. It only reads supplied files.
        Assert.DoesNotContain("[DllImport", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Start-Process", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invoke-Expression", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Add-Type", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Get-CimInstance", source, StringComparison.OrdinalIgnoreCase);
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
