using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace ThinkControl.UI.Services;

internal sealed record ModeAutomationSnapshot(
    string? WifiSsid,
    bool OnBattery,
    int BatteryPercent,
    IReadOnlySet<string> RunningProcesses,
    DateTimeOffset Now);

internal static class ThinkControlModeAutomationPolicy
{
    internal static int MatchScore(ThinkControlModeDefinition mode, ModeAutomationSnapshot snapshot)
    {
        if (!mode.AutomationEnabled)
            return 0;

        int score = 0;
        foreach (ThinkControlModeTrigger trigger in ThinkControlModeCatalog.SanitizeTriggers(mode.Triggers))
        {
            if (!trigger.Enabled || !Matches(trigger, snapshot))
                continue;
            score = Math.Max(score, TriggerScore(trigger.Type));
        }
        return score;
    }

    internal static bool Matches(ThinkControlModeTrigger trigger, ModeAutomationSnapshot snapshot) =>
        trigger.Type switch
        {
            "Wifi" => !string.IsNullOrWhiteSpace(snapshot.WifiSsid) &&
                      snapshot.WifiSsid.Equals(trigger.Value, StringComparison.OrdinalIgnoreCase),
            "Process" => snapshot.RunningProcesses.Contains(NormalizeProcess(trigger.Value)),
            "Power" => trigger.Value == "Battery" ? snapshot.OnBattery : !snapshot.OnBattery,
            "BatteryBelow" => snapshot.OnBattery &&
                              snapshot.BatteryPercent > 0 &&
                              snapshot.BatteryPercent <= trigger.Number,
            "Schedule" => MatchesSchedule(trigger, snapshot.Now.ToLocalTime()),
            _ => false
        };

    private static int TriggerScore(string type) => type switch
    {
        "Process" => 500,
        "Wifi" => 400,
        "BatteryBelow" => 320,
        "Power" => 300,
        "Schedule" => 200,
        _ => 0
    };

    private static bool MatchesSchedule(ThinkControlModeTrigger trigger, DateTimeOffset now)
    {
        if (!TimeOnly.TryParse(trigger.StartTime, out TimeOnly start) ||
            !TimeOnly.TryParse(trigger.EndTime, out TimeOnly end) ||
            start == end)
        {
            return false;
        }

        TimeOnly current = TimeOnly.FromDateTime(now.DateTime);
        int todayBit = 1 << (int)now.DayOfWeek;
        if (start < end)
            return (trigger.DaysMask & todayBit) != 0 && current >= start && current < end;

        if (current >= start)
            return (trigger.DaysMask & todayBit) != 0;

        DayOfWeek previousDay = (DayOfWeek)(((int)now.DayOfWeek + 6) % 7);
        int previousBit = 1 << (int)previousDay;
        return current < end && (trigger.DaysMask & previousBit) != 0;
    }

    private static string NormalizeProcess(string value)
    {
        string file = Path.GetFileName(value.Trim());
        return file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? file[..^4]
            : file;
    }
}

internal sealed class ThinkControlModeAutomationService : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private int _evaluating;
    private bool _started;
    private string? _lastCandidateId;
    private bool _manualOverride;
    private string? _manualOverrideCandidateId;

    internal ThinkControlModeAutomationService(App app)
    {
        _app = app;
        _timer = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timer.Tick += Timer_Tick;
    }

    internal void Start()
    {
        if (_started)
            return;
        _started = true;
        _timer.Start();
        RequestEvaluation();
    }

    internal void Stop()
    {
        _started = false;
        _timer.Stop();
    }

    internal void SuppressUntilContextChanges()
    {
        _manualOverride = true;
        _manualOverrideCandidateId = _lastCandidateId;
    }

    internal void RequestEvaluation()
    {
        if (!_started)
            return;
        _app.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(async () =>
        {
            await EvaluateAsync();
        }));
    }

    private async void Timer_Tick(object? sender, EventArgs e) => await EvaluateAsync();

    private async Task EvaluateAsync()
    {
        if (!_started || Interlocked.Exchange(ref _evaluating, 1) != 0)
            return;

        try
        {
            ThinkControlModeDefinition[] modes = _app.UserSettings.Current.CustomModes ?? [];
            if (modes.Length == 0 || !modes.Any(mode => mode.AutomationEnabled))
            {
                await ApplyCandidateAsync(null);
                return;
            }

            bool onBattery = _app.IsCurrentlyOnBattery();
            int batteryPercent = _app.State.BatteryPercent;

            ModeEnvironmentData environment = await Task.Run(ModeTriggerEnvironment.Capture);
            var snapshot = new ModeAutomationSnapshot(
                environment.WifiSsid,
                onBattery,
                batteryPercent,
                environment.RunningProcesses,
                DateTimeOffset.Now);

            ThinkControlModeDefinition? candidate = null;
            int bestScore = 0;
            foreach (ThinkControlModeDefinition mode in modes)
            {
                int score = ThinkControlModeAutomationPolicy.MatchScore(mode, snapshot);
                if (score <= bestScore)
                    continue;
                bestScore = score;
                candidate = mode;
            }

            await ApplyCandidateAsync(candidate);
        }
        finally
        {
            Interlocked.Exchange(ref _evaluating, 0);
        }
    }

    private async Task ApplyCandidateAsync(ThinkControlModeDefinition? candidate)
    {
        string? candidateId = candidate?.Id;

        if (_manualOverride)
        {
            if (_manualOverrideCandidateId is null)
            {
                _manualOverrideCandidateId = candidateId;
                _lastCandidateId = candidateId;
                return;
            }

            if (string.Equals(candidateId, _manualOverrideCandidateId, StringComparison.OrdinalIgnoreCase))
            {
                _lastCandidateId = candidateId;
                return;
            }

            _manualOverride = false;
            _manualOverrideCandidateId = null;
        }

        if (string.Equals(candidateId, _lastCandidateId, StringComparison.OrdinalIgnoreCase))
            return;

        if (candidate is not null)
        {
            bool applied = await _app.Modes.ActivateAsync(
                candidate.Id,
                ThinkControlModeActivationOrigin.Automatic);
            if (applied)
                _lastCandidateId = candidate.Id;
            return;
        }

        // If the context that automatically activated a mode disappears, restore the
        // user's regular settings. A manually selected mode is left alone.
        if (_app.Modes.ActiveModeAutomatic &&
            _app.Modes.ActiveModeId != ThinkControlModeCatalog.NormalId)
        {
            bool restored = await _app.Modes.ActivateAsync(
                ThinkControlModeCatalog.NormalId,
                ThinkControlModeActivationOrigin.Automatic);
            if (!restored)
                return;
        }

        _lastCandidateId = null;
    }

    public void Dispose()
    {
        Stop();
        _timer.Tick -= Timer_Tick;
    }
}

internal sealed record ModeEnvironmentData(
    string? WifiSsid,
    IReadOnlySet<string> RunningProcesses);

internal static class ModeTriggerEnvironment
{
    private const uint WlanClientVersion = 2;
    private const int WlanIntfOpcodeCurrentConnection = 7;
    private const int ConnectionAttributesAssociationOffset = 520;

    internal static ModeEnvironmentData Capture() =>
        new(GetConnectedWifiSsid(), GetRunningProcesses());

    private static IReadOnlySet<string> GetRunningProcesses()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(process.ProcessName))
                            result.Add(process.ProcessName);
                    }
                    catch
                    {
                    }
                }
            }
        }
        catch
        {
        }
        return result;
    }

    internal static string? GetConnectedWifiSsid()
    {
        IntPtr client = IntPtr.Zero;
        IntPtr interfaces = IntPtr.Zero;
        try
        {
            if (WlanOpenHandle(WlanClientVersion, IntPtr.Zero, out _, out client) != 0 ||
                client == IntPtr.Zero)
            {
                return null;
            }

            if (WlanEnumInterfaces(client, IntPtr.Zero, out interfaces) != 0 ||
                interfaces == IntPtr.Zero)
            {
                return null;
            }

            int count = Marshal.ReadInt32(interfaces);
            IntPtr current = IntPtr.Add(interfaces, 8);
            int size = Marshal.SizeOf<WlanInterfaceInfo>();
            for (int index = 0; index < count; index++)
            {
                WlanInterfaceInfo info =
                    Marshal.PtrToStructure<WlanInterfaceInfo>(IntPtr.Add(current, index * size));
                string? ssid = QuerySsid(client, info.InterfaceGuid);
                if (!string.IsNullOrWhiteSpace(ssid))
                    return ssid;
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch
        {
        }
        finally
        {
            if (interfaces != IntPtr.Zero)
                WlanFreeMemory(interfaces);
            if (client != IntPtr.Zero)
                WlanCloseHandle(client, IntPtr.Zero);
        }
        return null;
    }

    private static string? QuerySsid(IntPtr client, Guid interfaceGuid)
    {
        IntPtr data = IntPtr.Zero;
        try
        {
            if (WlanQueryInterface(
                    client,
                    ref interfaceGuid,
                    WlanIntfOpcodeCurrentConnection,
                    IntPtr.Zero,
                    out uint dataSize,
                    out data,
                    out _) != 0 ||
                data == IntPtr.Zero)
            {
                return null;
            }

            if (dataSize >= ConnectionAttributesAssociationOffset + 36)
            {
                IntPtr association = IntPtr.Add(data, ConnectionAttributesAssociationOffset);
                int length = Marshal.ReadInt32(association);
                if (length is > 0 and <= 32)
                {
                    byte[] bytes = new byte[length];
                    Marshal.Copy(IntPtr.Add(association, 4), bytes, 0, length);
                    string ssid = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                    if (!string.IsNullOrWhiteSpace(ssid))
                        return ssid;
                }
            }

            // Some drivers expose a profile even when association details are sparse.
            string? profile = Marshal.PtrToStringUni(IntPtr.Add(data, 8), 256)?.TrimEnd('\0').Trim();
            return string.IsNullOrWhiteSpace(profile) ? null : profile;
        }
        finally
        {
            if (data != IntPtr.Zero)
                WlanFreeMemory(data);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanInterfaceInfo
    {
        public Guid InterfaceGuid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string InterfaceDescription;

        public int State;
    }

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(
        uint clientVersion,
        IntPtr reserved,
        out uint negotiatedVersion,
        out IntPtr clientHandle);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr clientHandle, IntPtr reserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(
        IntPtr clientHandle,
        IntPtr reserved,
        out IntPtr interfaceList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanQueryInterface(
        IntPtr clientHandle,
        ref Guid interfaceGuid,
        int opcode,
        IntPtr reserved,
        out uint dataSize,
        out IntPtr data,
        out int opcodeValueType);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr memory);
}
