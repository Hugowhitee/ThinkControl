using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class StartupResponsivenessSourceTests
{
    [Fact]
    public void WindowsTrayStartup_DoesNotPutFullWmiInventoryOnTheUiCriticalPath()
    {
        string root = FindRepositoryRoot();
        string shell = Read(root, "src", "ThinkControl.UI", "App.ShellIcons.cs");
        string status = Read(root, "src", "ThinkControl.UI", "Services", "SystemStatusService.cs");
        string app = Read(root, "src", "ThinkControl.UI", "App.xaml.cs");

        Assert.Contains("SystemStatusService.UseFastStartupReadOnce();", shell, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _fastStartupReadPending, 0) == 1", status, StringComparison.Ordinal);
        Assert.Contains("BuildFastStartupSnapshot", status, StringComparison.Ordinal);
        Assert.Contains("Registry.LocalMachine.OpenSubKey(BiosRegistryPath", status, StringComparison.Ordinal);
        Assert.Contains("SystemStatusSnapshot system = await Task.Run(SystemStatusService.Read);", app, StringComparison.Ordinal);

        // Rich inventory remains available, but only the normal background read owns WMI.
        Assert.Contains("new ManagementObjectSearcher", status, StringComparison.Ordinal);
        string fastSection = status.Split("private SystemStatusSnapshot BuildFastStartupSnapshot", StringSplitOptions.None)[1]
            .Split("private static (int Battery, string Status) ReadPowerState", StringSplitOptions.None)[0];
        Assert.DoesNotContain("ManagementObjectSearcher", fastSection, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadFirst(", fastSection, StringComparison.Ordinal);
    }

    [Fact]
    public void EnabledEdgeGestures_StartAtTheEarliestTrayStartupHookBeforeShellDiscovery()
    {
        string root = FindRepositoryRoot();
        string startup = Read(root, "src", "ThinkControl.UI", "Services", "StartupService.cs");
        string shell = Read(root, "src", "ThinkControl.UI", "App.ShellIcons.cs");
        string touchpad = Read(root, "src", "ThinkControl.UI", "App.Touchpad.cs");
        string host = Read(root, "src", "ThinkControl.UI", "Services", "Touchpad", "TouchpadFeatureHost.cs");

        Assert.Contains("CurrentVersion\\Run", startup, StringComparison.Ordinal);
        Assert.Contains("--tray", startup, StringComparison.Ordinal);
        Assert.Contains("StartupSystemIdentity identity = SystemStatusService.ReadStartupIdentity();", shell, StringComparison.Ordinal);
        Assert.Contains("StartConfiguredTouchpadInputForStartup();", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherPriority.Background, new Action(StartConfiguredTouchpadInputForStartup)", shell, StringComparison.Ordinal);

        string startupMethod = touchpad.Split("internal void StartConfiguredTouchpadInputForStartup()", StringSplitOptions.None)[1]
            .Split("private void OnTouchpadApplicationActivated", StringSplitOptions.None)[0];
        Assert.Contains("UserSettings.Current.TouchpadGestures?.Enabled != true", startupMethod, StringComparison.Ordinal);
        Assert.Contains("TouchpadFeature.EnsureInputStarted(startupCritical: IsTrayOnlyLaunch())", startupMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("OnTouchpadApplicationActivated(", startupMethod, StringComparison.Ordinal);

        // The tray path starts Raw Input synchronously after cheap registry identity,
        // mirroring the essential-input-first discipline of lightweight helper apps.
        Assert.Contains("if (startupCritical)", host, StringComparison.Ordinal);
        Assert.Contains("return !_disposed && _gestures.Start();", host, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.ContextIdle", host, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherPriority.Background", host, StringComparison.Ordinal);

        // Activation remains a recovery path for device/session transitions.
        Assert.Contains("private void OnTouchpadApplicationActivated", touchpad, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] path) =>
        File.ReadAllText(Path.Combine([root, .. path]));

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

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for startup validation.");
    }
}
