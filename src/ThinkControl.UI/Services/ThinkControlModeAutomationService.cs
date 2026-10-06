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

        bool matched = false;
        foreach (ThinkControlModeTrigger trigger in triggers)
        {
            if (!Matches(trigger, snapshot))
            {
                if (mode.MatchAllTriggers)
                    return 0;
                continue;
            }
            matched = true;
        }

        // Equal-priority rules keep their saved, visible list order.
        return matched ? (mode.AutomationPriority + 1) * 1000 + 1 : 0;
    }

    internal static bool Matches(ThinkControlModeTrigger trigger, ModeAutomationSnapshot snapshot) =>
        trigger.Type switch
        {
            "Wifi" => !string.IsNullOrWhiteSpace(snapshot.WifiSsid) &&
                      snapshot.WifiSsid.Equals(trigger.Value, StringComparison.Ordinal),
            "Process" => snapshot.RunningProcesses.Contains(NormalizeProcess(trigger.Value)),
            "Power" => trigger.Value == "Battery" ? snapshot.OnBattery : !snapshot.OnBattery,
            "BatteryBelow" => snapshot.OnBattery &&
                              snapshot.BatteryPercent > 0 &&
                              snapshot.BatteryPercent <= trigger.Number,
            "Schedule" => MatchesSchedule(trigger, snapshot.Now.ToLocalTime()),
            _ => false
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

internal sealed record AutomationRuleMatch(string Id, string Name, string ModeName, bool Matches, string State);

internal sealed class ThinkControlModeAutomationService : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private int _evaluating;
    private bool _started;
    private string? _activeRuleId;
    private ThinkControlModeDefinition? _appliedDefinition;
    private ThinkControlModeCoordinator.Session? _beforeAutomation;
    private string? _pendingRuleId;
    private bool _pendingSet;
    private DateTimeOffset _pendingSince;
    private string? _failedRuleId;
    private DateTimeOffset _failedUntil;
    private bool _manualOverride;
    private string? _manualContextId;
    private string? _observedWinnerId;
    internal event Action? Changed;
    internal string Status { get; private set; } = "Checking conditions…";
    internal bool Paused => _manualOverride;
    internal string RestoreTarget => _beforeAutomation?.Definition.Name ?? "Regular settings";
    internal IReadOnlyList<AutomationRuleMatch> Matches { get; private set; } = [];

    internal ThinkControlModeAutomationService(App app)
    {
        _app = app;
        _timer = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher) { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += Timer_Tick;
    }

    internal void Start() { if (_started) return; _started = true; _timer.Start(); RequestEvaluation(); }
    internal void Stop() { _started = false; _timer.Stop(); }
    internal void SuppressUntilContextChanges()
    {
        _manualOverride = true;
        // Use the latest observed context even during the dwell period.
        _manualContextId = _observedWinnerId;
        _beforeAutomation = null;
        _activeRuleId = null;
        SetStatus("Paused by your manual selection. Resume, or wait for a different rule to win.");
    }
    internal void Resume()
    {
        _manualOverride = false;
        _failedRuleId = null;
        _pendingSet = false;
        RequestEvaluation();
    }
    internal void ReleaseRestoreFacet(ThinkControlModeFacet facet)
    {
        if (_beforeAutomation is not null)
            _beforeAutomation = _beforeAutomation with { Definition = ThinkControlModeCoordinator.WithoutFacet(_beforeAutomation.Definition, facet), Modified = true };
    }
    private static bool SameSettings(ThinkControlModeDefinition? first, ThinkControlModeDefinition? second) =>
        first is not null && second is not null && first.Id == second.Id &&
        first.AudioSafety == second.AudioSafety && first.TouchpadGesturesEnabled == second.TouchpadGesturesEnabled &&
        first.KeyboardLight == second.KeyboardLight && first.PerformanceMode == second.PerformanceMode &&
        first.CoolingProfile == second.CoolingProfile && first.RefreshRate == second.RefreshRate;
    internal void RequestEvaluation()
    {
        if (!_started) return;
        _app.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(async () => await EvaluateAsync()));
    }
    private async void Timer_Tick(object? sender, EventArgs e) => await EvaluateAsync();
    private async Task EvaluateAsync()
    {
        if (!_started || Interlocked.Exchange(ref _evaluating, 1) != 0) return;
        try
        {
            var environment = await Task.Run(ModeTriggerEnvironment.Capture);
            if (!environment.WifiKnown && (_app.UserSettings.Current.AutomationRules ?? [])
                .Any(rule => rule.Enabled && rule.Conditions.Any(condition => condition.Enabled && condition.Type == "Wifi")))
            {
                SetStatus("Wi-Fi could not be checked. Current settings are kept; check Windows Wi-Fi permissions.");
                _pendingSet = false;
                return;
            }
            await EvaluateSnapshotAsync(new ModeAutomationSnapshot(environment.WifiSsid,
                _app.IsCurrentlyOnBattery(), _app.State.BatteryPercent, environment.RunningProcesses, DateTimeOffset.Now));
        }
        catch (Exception)
        {
            SetStatus("Conditions could not be checked. Current settings are kept; retrying shortly.");
        }
        finally { Interlocked.Exchange(ref _evaluating, 0); }
    }

    // The installed runtime and dispatcher tests use the same transition path.
    internal async Task EvaluateSnapshotAsync(ModeAutomationSnapshot context)
    {
        var modes = _app.UserSettings.Current.CustomModes ?? [];
        var rules = _app.UserSettings.Current.AutomationRules ?? [];
        var ranked = rules.Select(rule => new
        {
            Rule = rule,
            Mode = ThinkControlModeCatalog.Find(rule.ModeId, modes),
            Score = ThinkControlModeAutomationPolicy.MatchScore(ThinkControlAutomationRules.AsPolicyMode(rule), context)
        }).ToArray();
        var winner = ranked.Where(item => item.Mode is not null && item.Score > 0)
            .OrderByDescending(item => item.Score)
            .FirstOrDefault();
        _observedWinnerId = winner?.Rule.Id;
        var matches = ranked.Select(item => new AutomationRuleMatch(item.Rule.Id, item.Rule.Name,
            item.Mode?.Name ?? "Missing mode", item.Score > 0,
            item.Mode is null ? "Choose a mode" : !item.Rule.Enabled ? "Disabled" :
            item.Score == 0 ? "Not matched" : item.Rule.Id == winner?.Rule.Id ? "Winner" :
            $"Matched; {winner!.Rule.Name} " + (winner.Rule.Priority > item.Rule.Priority ? "has higher priority" : "comes first"))).ToArray();
        if (!Matches.SequenceEqual(matches)) { Matches = matches; Changed?.Invoke(); }

        if (_manualOverride && _observedWinnerId == _manualContextId)
        {
            SetStatus("Paused by your manual selection. Resume, or wait for a different rule to win.");
            return;
        }
        if (_manualOverride) { _manualOverride = false; _manualContextId = null; }

        if (!_pendingSet || _pendingRuleId != _observedWinnerId)
        {
            _pendingSet = true;
            _pendingRuleId = _observedWinnerId;
            _pendingSince = context.Now;
            SetStatus(winner is null ? (_beforeAutomation is null ? "No rules match." : $"Waiting to restore {RestoreTarget}…")
                : $"Waiting for {winner.Rule.Name} to remain matched…");
            return;
        }
        if (context.Now - _pendingSince < TimeSpan.FromSeconds(5)) return;

        string transitionKey = winner?.Rule.Id ?? "restore";
        if (_failedRuleId == transitionKey && context.Now < _failedUntil) return;
        if (winner is not null)
        {
            bool definitionChanged = !SameSettings(_appliedDefinition, winner.Mode);
            if (_activeRuleId == winner.Rule.Id && !definitionChanged)
            {
                PublishWinner(winner.Rule, context);
                return;
            }
            var previous = _beforeAutomation ?? _app.Modes.CaptureSession();
            // Changing the winning rule to the same unchanged mode is arbitration,
            // not permission to reclaim facets the user manually released.
            bool alreadyApplied = _app.Modes.ActiveModeAutomatic && SameSettings(_appliedDefinition, winner.Mode);
            if (alreadyApplied || await _app.Modes.ActivateAsync(winner.Mode!.Id, ThinkControlModeActivationOrigin.Automatic))
            {
                _beforeAutomation = previous;
                _activeRuleId = winner.Rule.Id;
                _appliedDefinition = winner.Mode;
                _failedRuleId = null;
                PublishWinner(winner.Rule, context);
            }
            else RecordFailure(transitionKey, context.Now);
            return;
        }
        if (_beforeAutomation is not null && _app.Modes.ActiveModeAutomatic)
        {
            if (!await _app.Modes.RestoreSessionAsync(_beforeAutomation))
            { RecordFailure(transitionKey, context.Now); return; }
            SetStatus($"Restored {_app.Modes.ActiveModeName}. No rules match.");
        }
        else if (!Status.StartsWith("Restored ", StringComparison.Ordinal)) SetStatus("No rules match.");
        _beforeAutomation = null;
        _activeRuleId = null;
        _appliedDefinition = null;
        _failedRuleId = null;
    }
    private void PublishWinner(ThinkControlAutomationRule rule, ModeAutomationSnapshot context)
    {
        var reasons = rule.Conditions.Where(condition => condition.Enabled && ThinkControlModeAutomationPolicy.Matches(condition, context))
            .Select(ThinkControlModeCatalog.TriggerSummary);
        SetStatus($"{rule.Name} activates {_app.Modes.ActiveModeName}: {string.Join(", ", reasons)}. Afterwards: {RestoreTarget}.");
    }
    private void RecordFailure(string id, DateTimeOffset now)
    {
        _failedRuleId = id;
        _failedUntil = now + TimeSpan.FromMinutes(1);
        SetStatus("Could not apply or restore the mode. " + _app.Modes.LastTransitionError + " Retrying in one minute.");
    }
    private void SetStatus(string value)
    {
        if (Status == value) return;
        Status = value;
        Changed?.Invoke();
    }
    public void Dispose() { Stop(); _timer.Tick -= Timer_Tick; }
}
internal sealed record ModeEnvironmentData(
    string? WifiSsid,
    IReadOnlySet<string> RunningProcesses, bool WifiKnown = true);

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

    internal static ModeEnvironmentData Capture()
    {
        string? ssid = GetConnectedWifiSsid(out bool known);
        return new(ssid, GetRunningProcesses(), known);
    }

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

    internal static string? GetConnectedWifiSsid() => GetConnectedWifiSsid(out _);
    private static string? GetConnectedWifiSsid(out bool known)
    {
        known = true;
        IntPtr client = IntPtr.Zero;
        IntPtr interfaces = IntPtr.Zero;
        try
        {
            if (WlanOpenHandle(WlanClientVersion, IntPtr.Zero, out _, out client) != 0 ||
                client == IntPtr.Zero)
            {
                known = false; return null;
            }

            if (WlanEnumInterfaces(client, IntPtr.Zero, out interfaces) != 0 ||
                interfaces == IntPtr.Zero)
            {
                known = false; return null;
            }

            int count = Math.Clamp(Marshal.ReadInt32(interfaces), 0, 16);
            IntPtr current = IntPtr.Add(interfaces, 8);
            int size = Marshal.SizeOf<WlanInterfaceInfo>();
            for (int index = 0; index < count; index++)
            {
                WlanInterfaceInfo info =
                    Marshal.PtrToStructure<WlanInterfaceInfo>(IntPtr.Add(current, index * size));
                if (info.State != 1) continue;
                string? ssid = QuerySsid(client, info.InterfaceGuid, out bool queryKnown);
                known &= queryKnown;
                if (!string.IsNullOrWhiteSpace(ssid))
                    return ssid;
            }
        }
        catch (DllNotFoundException)
        {
            known = false;
        }
        catch (EntryPointNotFoundException)
        {
            known = false;
        }
        catch
        {
            known = false;
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

    private static string? QuerySsid(IntPtr client, Guid interfaceGuid, out bool known)
    {
        known = true;
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
                known = false; return null;
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

            // A profile alias is not an observed SSID; unknown must not masquerade as disconnection.
            known = false; return null;
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
