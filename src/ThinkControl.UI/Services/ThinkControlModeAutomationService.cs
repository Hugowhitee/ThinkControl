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

        ThinkControlModeTrigger[] triggers = ThinkControlModeCatalog.SanitizeTriggers(mode.Triggers)
            .Where(trigger => trigger.Enabled)
            .ToArray();
        if (triggers.Length == 0)
            return 0;

        int score = 0;
        foreach (ThinkControlModeTrigger trigger in triggers)
        {
            if (!Matches(trigger, snapshot))
            {
                if (mode.MatchAllTriggers)
                    return 0;
                continue;
            }
            score = Math.Max(score, TriggerScore(trigger.Type));
        }

        // Priority is user controlled; the type rank only resolves equally
        // prioritized matches. No overlapping modes are stacked.
        return score == 0 ? 0 : (mode.AutomationPriority + 1) * 1000 + score;
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
    private string? _modeBeforeAutomationId;
    private string? _pendingCandidateId;
    private DateTimeOffset _pendingCandidateSince = DateTimeOffset.MinValue;
    private bool _pendingCandidateSet;
    private static readonly TimeSpan CandidateDwell = TimeSpan.FromSeconds(5);
    private string? _failedCandidateId;
    private DateTimeOffset _failedCandidateUntil;
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
        _modeBeforeAutomationId = null; // The user just made an explicit choice.
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
                if (score < bestScore || score == 0)
                    continue;
                if (score == bestScore &&
                    !string.Equals(mode.Id, _lastCandidateId, StringComparison.OrdinalIgnoreCase))
                    continue;
                bestScore = score;
                candidate = mode;
            }

            // A 5-second stable dwell avoids Wi-Fi roaming or short-lived app
            // probes bouncing Quiet/Performance and damaging the restore chain.
            string? id = candidate?.Id;
            if (!_pendingCandidateSet ||
                !string.Equals(id, _pendingCandidateId, StringComparison.OrdinalIgnoreCase))
            {
                _pendingCandidateSet = true;
                _pendingCandidateId = id;
                _pendingCandidateSince = DateTimeOffset.UtcNow;
                return;
            }
            if (DateTimeOffset.UtcNow - _pendingCandidateSince < CandidateDwell)
                return;

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
            // Null is a valid previous context (no matching trigger at home).
            // Do not absorb the *first* new school Wi-Fi match as the manual
            // override baseline; that would suppress School until a second
            // unrelated context change occurred.
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
            // An unavailable hardware provider must not cause retries every five seconds.
            if (string.Equals(candidate.Id, _failedCandidateId, StringComparison.OrdinalIgnoreCase) &&
                DateTimeOffset.UtcNow < _failedCandidateUntil)
                return;

            string? previous = _app.Modes.ActiveModeAutomatic
                ? _modeBeforeAutomationId
                : _app.Modes.ActiveModeId;
            bool applied = await _app.Modes.ActivateAsync(
                candidate.Id,
                ThinkControlModeActivationOrigin.Automatic);
            if (applied)
            {
                _modeBeforeAutomationId = previous;
                _lastCandidateId = candidate.Id;
                _failedCandidateId = null;
            }
            else
            {
                _failedCandidateId = candidate.Id;
                _failedCandidateUntil = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1);
            }
            return;
        }

        // If the context that automatically activated a mode disappears, restore the
        // user's regular settings. A manually selected mode is left alone.
        if (_app.Modes.ActiveModeAutomatic &&
            _app.Modes.ActiveModeId != ThinkControlModeCatalog.NormalId)
        {
            // Leaving school (or another trigger) returns to the explicit
            // pre-automation mode, otherwise the original normal settings.
            string restoreId = _modeBeforeAutomationId ?? ThinkControlModeCatalog.NormalId;
            if (ThinkControlModeCatalog.Find(restoreId, _app.UserSettings.Current.CustomModes) is null)
                restoreId = ThinkControlModeCatalog.NormalId;
            if (string.Equals(restoreId, _failedCandidateId, StringComparison.OrdinalIgnoreCase) &&
                DateTimeOffset.UtcNow < _failedCandidateUntil)
                return;
            bool restored = await _app.Modes.ActivateAsync(
                restoreId,
                ThinkControlModeActivationOrigin.Restore);
            if (!restored)
            {
                _failedCandidateId = restoreId;
                _failedCandidateUntil = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1);
                return;
            }
        }

        _lastCandidateId = null;
        _modeBeforeAutomationId = null;
        _failedCandidateId = null;
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
    private const int MaxSuggestedNetworks = 7;
    private const uint WlanClientVersion = 2;

    // Local WLAN profile names only (no passwords, no scan of nearby networks).
    // Windows orders profiles by preference, not by last-connected timestamp.
    internal static IReadOnlyList<string> SuggestedWifiNetworks()
    {
        var names = new List<string>(MaxSuggestedNetworks);
        string? connected = GetConnectedWifiSsid();
        if (!string.IsNullOrWhiteSpace(connected))
            names.Add(connected);

        IntPtr client = IntPtr.Zero;
        IntPtr interfaces = IntPtr.Zero;
        try
        {
            if (WlanOpenHandle(WlanClientVersion, IntPtr.Zero, out _, out client) != 0 ||
                client == IntPtr.Zero ||
                WlanEnumInterfaces(client, IntPtr.Zero, out interfaces) != 0 ||
                interfaces == IntPtr.Zero)
                return names;

            int count = Math.Clamp(Marshal.ReadInt32(interfaces), 0, 16);
            IntPtr current = IntPtr.Add(interfaces, 8);
            int size = Marshal.SizeOf<WlanInterfaceInfo>();
            for (int index = 0; index < count && names.Count < MaxSuggestedNetworks; index++)
            {
                WlanInterfaceInfo info = Marshal.PtrToStructure<WlanInterfaceInfo>(
                    IntPtr.Add(current, index * size));
                IntPtr profiles = IntPtr.Zero;
                try
                {
                    if (WlanGetProfileList(client, ref info.InterfaceGuid, IntPtr.Zero, out profiles) != 0 ||
                        profiles == IntPtr.Zero)
                        continue;
                    int profileCount = Math.Clamp(Marshal.ReadInt32(profiles), 0, 128);
                    IntPtr start = IntPtr.Add(profiles, 8);
                    int stride = Marshal.SizeOf<WlanProfileInfo>();
                    for (int profileIndex = 0;
                         profileIndex < profileCount && names.Count < MaxSuggestedNetworks;
                         profileIndex++)
                    {
                        WlanProfileInfo profile = Marshal.PtrToStructure<WlanProfileInfo>(
                            IntPtr.Add(start, profileIndex * stride));
                        string name = profile.Name?.Trim() ?? string.Empty;
                        if (name.Length > 0 && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                            names.Add(name);
                    }
                }
                finally
                {
                    if (profiles != IntPtr.Zero)
                        WlanFreeMemory(profiles);
                }
            }
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        catch { }
        finally
        {
            if (interfaces != IntPtr.Zero)
                WlanFreeMemory(interfaces);
            if (client != IntPtr.Zero)
                WlanCloseHandle(client, IntPtr.Zero);
        }
        return names;
    }
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
    private struct WlanProfileInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Name;
        public uint Flags;
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
    private static extern uint WlanGetProfileList(
        IntPtr clientHandle,
        ref Guid interfaceGuid,
        IntPtr reserved,
        out IntPtr profileList);

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
