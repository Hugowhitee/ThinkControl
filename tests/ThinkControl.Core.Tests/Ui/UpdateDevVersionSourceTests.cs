using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class UpdateDevVersionSourceTests
{
    [Fact]
    public void SameBasePublishedReleaseSupersedesDevelopmentBuild()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "UpdateService.cs"));

        Assert.Contains("LastIndexOf(\"-dev.\"", source, StringComparison.Ordinal);
        Assert.Contains("developmentBuild = parsedDevelopmentBuild", source, StringComparison.Ordinal);
        Assert.Contains("return DevelopmentBuild.HasValue ? -1 : 1", source, StringComparison.Ordinal);
        Assert.Contains("return leftDev.CompareTo(rightDev)", source, StringComparison.Ordinal);

        // Guard the exact failure mode seen with CI packages such as
        // alpha.50-dev.1767: canonical alpha.50 must not be hidden by SemVer's
        // ordinary numeric-vs-nonnumeric prerelease ordering.
        Assert.Contains("0.1.0-alpha.50-dev.1767", source, StringComparison.Ordinal);
        Assert.Contains("same base prerelease", source, StringComparison.OrdinalIgnoreCase);
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

        throw new DirectoryNotFoundException("Could not locate ThinkControl repository root for updater dev-version validation.");
    }
}
