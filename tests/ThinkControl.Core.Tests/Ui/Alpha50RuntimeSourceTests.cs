using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class Alpha50RuntimeSourceTests
{
    [Fact]
    public void FanSelection_KeepsPendingUserIntentUntilServiceConverges()
    {
        string cooling = Read("src", "ThinkControl.UI", "App.Cooling.cs");
        string diagnostics = Read("src", "ThinkControl.UI", "App.Diagnostics.cs");
        string service = Read("src", "ThinkControl.Service", "ServiceEngine.cs");

        Assert.Contains("SetPendingCoolingProfile(generation, pendingDisplay)", cooling, StringComparison.Ordinal);
        Assert.Contains("TryGetPendingCoolingProfile(out string pendingCooling)", diagnostics, StringComparison.Ordinal);
        Assert.Contains("? pendingCooling", diagnostics, StringComparison.Ordinal);
        Assert.Contains("ClearPendingCoolingProfile(generation)", cooling, StringComparison.Ordinal);
        Assert.Contains("_ = HardwareClient.GetStatusAsync();", cooling, StringComparison.Ordinal);

        string autoMethod = Normalize(service)
            .Split("private ServiceResponse ReturnFanToAuto()", StringSplitOptions.None)[1]
            .Split("private ServiceResponse SetCoolingProfile", StringSplitOptions.None)[0];
        Assert.Contains("if (firmware.OverrideActive)", autoMethod, StringComparison.Ordinal);
        Assert.Contains("_coolingPolicy.RequestFirmwareAuto", autoMethod, StringComparison.Ordinal);
        Assert.Contains("CoolingSupervisorSnapshot direct = _fanSupervisor.Snapshot()", autoMethod, StringComparison.Ordinal);

        int policyIndex = autoMethod.IndexOf("_coolingPolicy.RequestFirmwareAuto", StringComparison.Ordinal);
        int directIndex = autoMethod.IndexOf("CoolingSupervisorSnapshot direct", StringComparison.Ordinal);
        Assert.True(policyIndex >= 0 && directIndex > policyIndex);
    }

    [Fact]
    public void KeyboardAudioEffect_DecodesExtensibleLoopbackAndRestartsCapture()
    {
        string service = Read("src", "ThinkControl.UI", "Services", "KeyboardEffectService.cs");

        Assert.Contains("format is WaveFormatExtensible extensible", service, StringComparison.Ordinal);
        Assert.Contains("extensible.ToStandardWaveFormat()", service, StringComparison.Ordinal);
        Assert.Contains("WaveFormatEncoding.IeeeFloat", service, StringComparison.Ordinal);
        Assert.Contains("WaveFormatEncoding.Pcm", service, StringComparison.Ordinal);
        Assert.Contains("_audioCapture = capture;", service, StringComparison.Ordinal);
        Assert.Contains("capture.StartRecording();", service, StringComparison.Ordinal);
        Assert.Contains("_audioRestartGeneration", service, StringComparison.Ordinal);
        Assert.Contains("await Task.Delay(250)", service, StringComparison.Ordinal);
        Assert.Contains("rms / peak >= 0.58", service, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomaticKeyboardEffects_HideOnlyLenovoBacklightOsdBurst()
    {
        string service = Read("src", "ThinkControl.UI", "Services", "KeyboardEffectService.cs");
        string suppressor = Read("src", "ThinkControl.UI", "Services", "LenovoKeyboardOsdSuppressor.cs");

        Assert.Contains("if (!force)", service, StringComparison.Ordinal);
        Assert.Contains("_osdSuppressor.Arm();", service, StringComparison.Ordinal);
        Assert.Contains("Process.GetProcessesByName(\"tposd\")", suppressor, StringComparison.Ordinal);
        Assert.Contains("ShowWindow(hwnd, SwHide)", suppressor, StringComparison.Ordinal);
        Assert.Contains("WatchWindow = TimeSpan.FromMilliseconds(700)", suppressor, StringComparison.Ordinal);
        Assert.Contains("ProcessCacheLifetime = TimeSpan.FromSeconds(30)", suppressor, StringComparison.Ordinal);
        Assert.DoesNotContain("LenovoUtility", suppressor, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string Read(params string[] path)
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
                if (Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.UI")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tests", "ThinkControl.Core.Tests")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ThinkControl repository root for alpha.50 runtime validation.");
    }
}
