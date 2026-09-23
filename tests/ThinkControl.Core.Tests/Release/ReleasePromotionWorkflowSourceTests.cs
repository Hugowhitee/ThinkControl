using Xunit;

namespace ThinkControl.Core.Tests.Release;

public sealed class ReleasePromotionWorkflowSourceTests
{
    [Fact]
    public void Promotion_RetriesTransientReleaseAssetPropagation()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "promote-release.yml"));

        Assert.Contains("Release asset metadata is present but downloads are not ready yet; retrying.", workflow, StringComparison.Ordinal);
        Assert.Contains("--dir /tmp/thinkcontrol-release-verify", workflow, StringComparison.Ordinal);
        Assert.Contains("if gh release download", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("cd /tmp/thinkcontrol-release-verify\n                gh release download", workflow, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? current = new(start);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".github", "workflows")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tests", "ThinkControl.Core.Tests")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ThinkControl repository root for release workflow validation.");
    }
}
