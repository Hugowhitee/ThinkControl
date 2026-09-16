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

    [Fact]
    public void ColdBootCoolingRestore_HasABoundedServiceConvergencePathEvenWhileTrayOnly()
    {
        string root = FindRepositoryRoot();
        string app = Read(root, "src", "ThinkControl.UI", "App.xaml.cs");
        string cooling = Read(root, "src", "ThinkControl.UI", "App.Cooling.cs");
        string client = Read(root, "src", "ThinkControl.UI", "Services", "HardwareServiceClient.cs");
        string runtime = Read(root, "src", "ThinkControl.UI", "App.RuntimeRefresh.cs");

        Assert.Contains("StartCoolingColdStartConvergence();", app, StringComparison.Ordinal);
        Assert.Contains("CoolingColdStartProbeDelays", cooling, StringComparison.Ordinal);
        Assert.Contains("ConvergeCoolingPreferenceAfterColdStartAsync", cooling, StringComparison.Ordinal);
        Assert.Contains("bypassOfflineBackoff: true", cooling, StringComparison.Ordinal);
        Assert.Contains("bypassRetryBackoff: true", cooling, StringComparison.Ordinal);
        Assert.Contains("(!bypassRetryBackoff && DateTimeOffset.UtcNow < _coolingPreferenceRetryAfter)", cooling, StringComparison.Ordinal);
        Assert.Contains("generation != Volatile.Read(ref _coolingSelectionGeneration)", cooling, StringComparison.Ordinal);
        string convergence = cooling.Split("private async Task ConvergeCoolingPreferenceAfterColdStartAsync", StringSplitOptions.None)[1]
            .Split("private async Task TryRestoreCoolingPreferenceAsync", StringSplitOptions.None)[0];
        int statusAwait = convergence.IndexOf("await HardwareClient.GetStatusAsync", StringComparison.Ordinal);
        int postAwaitGenerationCheck = convergence.IndexOf(
            "generation != Volatile.Read(ref _coolingSelectionGeneration)",
            statusAwait,
            StringComparison.Ordinal);
        int restoreCall = convergence.IndexOf(
            "await TryRestoreCoolingPreferenceAsync(response, bypassRetryBackoff: true)",
            StringComparison.Ordinal);
        Assert.True(statusAwait >= 0 && postAwaitGenerationCheck > statusAwait && restoreCall > postAwaitGenerationCheck);
        Assert.Contains("_coolingPreferenceRestoreAttempted", cooling, StringComparison.Ordinal);
        Assert.Contains("SemaphoreSlim _coolingWriteGate", cooling, StringComparison.Ordinal);
        Assert.Contains("int generation = Interlocked.Increment(ref _coolingSelectionGeneration);", cooling, StringComparison.Ordinal);
        Assert.Contains("await _coolingWriteGate.WaitAsync();", cooling, StringComparison.Ordinal);
        Assert.Contains("await _coolingWriteGate.WaitAsync(_coolingLifetimeCts.Token);", cooling, StringComparison.Ordinal);
        Assert.Contains("await _coolingWriteGate.WaitAsync(cancellationToken);", cooling, StringComparison.Ordinal);

        string userSelection = cooling.Split("internal async Task<bool> SetCoolingProfileAsync", StringSplitOptions.None)[1]
            .Split("private async Task<bool> SetCoolingProfileCoreAsync", StringSplitOptions.None)[0];
        int userGeneration = userSelection.IndexOf("Interlocked.Increment(ref _coolingSelectionGeneration)", StringComparison.Ordinal);
        int userGateWait = userSelection.IndexOf("await _coolingWriteGate.WaitAsync()", StringComparison.Ordinal);
        Assert.True(userGeneration >= 0 && userGateWait > userGeneration);

        string restore = cooling.Split("private async Task TryRestoreCoolingPreferenceAsync", StringSplitOptions.None)[1]
            .Split("private void ScheduleFirmwareCoolingSettleReassert", StringSplitOptions.None)[0];
        int restoreGateWait = restore.IndexOf("await _coolingWriteGate.WaitAsync(_coolingLifetimeCts.Token)", StringComparison.Ordinal);
        int restoreGenerationCheck = restore.IndexOf(
            "generation != Volatile.Read(ref _coolingSelectionGeneration)",
            restoreGateWait,
            StringComparison.Ordinal);
        int restoreHardwareWrite = restore.IndexOf("HardwareClient.ReturnFanToAutoAsync()", StringComparison.Ordinal);
        if (restoreHardwareWrite < 0)
            restoreHardwareWrite = restore.IndexOf("HardwareClient.SetThermalModeAsync(State.SelectedMode)", StringComparison.Ordinal);
        Assert.True(restoreGateWait >= 0 && restoreGenerationCheck > restoreGateWait && restoreHardwareWrite > restoreGenerationCheck);

        Assert.Contains("bool bypassOfflineBackoff = false", client, StringComparison.Ordinal);
        Assert.Contains("if (!bypassOfflineBackoff && now < _offlineRetryAfter)", client, StringComparison.Ordinal);

        // Keep the normal tray runtime sparse; the cold-start convergence is bounded
        // rather than turning background hardware discovery into permanent polling.
        Assert.Contains("RuntimeBatteryTrayInterval = TimeSpan.FromMinutes(1)", runtime, StringComparison.Ordinal);
        Assert.Contains("ShouldRefreshHardwareRuntime()", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void ColdBootCooling_UsesRealThermalBaselineAndQuitAwaitsDirectWriterHandoff()
    {
        string root = FindRepositoryRoot();
        string app = Read(root, "src", "ThinkControl.UI", "App.xaml.cs");
        string cooling = Read(root, "src", "ThinkControl.UI", "App.Cooling.cs");

        int modeRead = app.IndexOf("ThinkControlPowerMode? mode = PowerModeService.GetCurrent(!battery.OnAc);", StringComparison.Ordinal);
        int baselineReady = app.IndexOf("MarkCoolingThermalBaselineReady();", modeRead, StringComparison.Ordinal);
        Assert.True(modeRead >= 0 && baselineReady > modeRead);

        Assert.Contains("private bool CoolingThermalBaselineReady", cooling, StringComparison.Ordinal);
        Assert.Contains("if (!wantsAuto && firmwarePolicy && !CoolingThermalBaselineReady)", cooling, StringComparison.Ordinal);

        string restore = cooling.Split("private async Task TryRestoreCoolingPreferenceAsync", StringSplitOptions.None)[1]
            .Split("private void ScheduleFirmwareCoolingSettleReassert", StringSplitOptions.None)[0];
        int baselineGate = restore.IndexOf("!CoolingThermalBaselineReady", StringComparison.Ordinal);
        int thermalWrite = restore.IndexOf("SetThermalModeAsync(State.SelectedMode, _coolingLifetimeCts.Token)", StringComparison.Ordinal);
        Assert.True(baselineGate >= 0 && thermalWrite > baselineGate);

        Assert.Contains("public async void ExitApplication()", app, StringComparison.Ordinal);
        Assert.Contains("await PrepareCoolingForApplicationExitAsync();", app, StringComparison.Ordinal);
        Assert.Contains("internal async Task PrepareCoolingForApplicationExitAsync()", cooling, StringComparison.Ordinal);
        Assert.Contains("_coolingLifetimeCts.Cancel();", cooling, StringComparison.Ordinal);
        Assert.Contains("await _coolingWriteGate.WaitAsync(timeout.Token);", cooling, StringComparison.Ordinal);
        Assert.Contains("await HardwareClient.ReturnFanToAutoAsync(timeout.Token);", cooling, StringComparison.Ordinal);
        Assert.Contains("SetCoolingCurveAsync(definition, _coolingLifetimeCts.Token)", cooling, StringComparison.Ordinal);
        Assert.Contains("SetCoolingCurveAsync(normalized, _coolingLifetimeCts.Token)", cooling, StringComparison.Ordinal);
        Assert.Contains("SetFanPercentAsync(percent, _coolingLifetimeCts.Token)", cooling, StringComparison.Ordinal);
        Assert.Contains("if (!_coolingWriteGate.Wait(0))", cooling, StringComparison.Ordinal);
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
