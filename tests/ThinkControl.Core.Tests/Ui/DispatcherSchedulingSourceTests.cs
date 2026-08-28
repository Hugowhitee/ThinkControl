using System.Text.RegularExpressions;
using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class DispatcherSchedulingSourceTests
{
    private static readonly Regex ActionThenPriority = new(
        @"Dispatcher\.BeginInvoke\s*\(\s*new\s+Action(?:<[^>]+>)?\s*\([\s\S]{0,400}?\)\s*,\s*DispatcherPriority\.",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MethodThenPriority = new(
        @"Dispatcher\.BeginInvoke\s*\(\s*[A-Za-z_][A-Za-z0-9_]*\s*,\s*DispatcherPriority\.",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void BeginInvokePriority_IsNeverPassedAsADelegateArgument()
    {
        string root = FindRepositoryRoot();
        string uiRoot = Path.Combine(root, "src", "ThinkControl.UI");
        var offenders = new List<string>();

        foreach (string file in Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            if (ActionThenPriority.IsMatch(source) || MethodThenPriority.IsMatch(source))
                offenders.Add(Path.GetRelativePath(root, file));
        }

        Assert.True(
            offenders.Count == 0,
            "WPF Dispatcher.BeginInvoke requires DispatcherPriority before the delegate. " +
            "The method-first form can bind DispatcherPriority into params object[] and later crash " +
            "through DynamicInvoke with TargetParameterCountException. Offenders: " +
            string.Join(", ", offenders));
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

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for dispatcher source validation.");
    }
}
