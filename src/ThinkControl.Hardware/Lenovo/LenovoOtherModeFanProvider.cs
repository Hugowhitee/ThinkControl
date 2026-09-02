using System.Collections;
using System.Management;

namespace ThinkControl.Hardware.Lenovo;

public enum LenovoFanControlKind
{
    None,
    ThinkPadEcDiscrete,
    LenovoOtherModeTargetRpm
}

internal sealed record LenovoOtherModeFanChannel(
    int Index,
    uint AttributeId,
    int MinRpm,
    int MaxRpm,
    uint Capability,
    bool CapabilityPresent);

internal sealed record LenovoOtherModeFanStatus(
    bool Available,
    bool CanControl,
    IReadOnlyList<LenovoFanReading> Fans,
    IReadOnlyList<LenovoOtherModeFanChannel> Channels,
    string Detail);

/// <summary>
/// Lenovo OEM fan provider coordinator.
///
/// Lenovo's upstream Other Mode contract exposes fanX_input plus a directly
/// tunable fanX_target through LENOVO_OTHER_METHOD. The fan attribute ID is
/// 0x04/0x03/0x00/<fan id>, SetFeatureValue writes a target RPM, firmware rounds
/// targets to its 100-RPM divisor, and target 0 hands the fan back to Auto.
///
/// Canonical discovery uses Capability Data 00 plus Fan Test Data. Exact X9
/// firmware is additionally allowed a narrow direct-ID fallback when Capability
/// Data omits a fan attribute: the known 0x0403000N ID must still answer a live
/// GetFeatureValue and that physical fan must have a sane Fan Test RPM range.
/// An explicitly present but invalid/readonly capability is never overridden.
///
/// Some Lenovo families may expose fan telemetry only through EnergyDrv. In that
/// case EnergyDrv remains read-only while the exact matching OEM writer is
/// recovered.
/// </summary>
internal sealed class LenovoOtherModeFanProvider
{
    private const string WmiNamespace = @"root\WMI";
    private const string MethodClass = "LENOVO_OTHER_METHOD";
    private const string CapabilityClass = "LENOVO_CAPABILITY_DATA_00";
    private const string FanTestClass = "LENOVO_FAN_TEST_DATA";

    private const uint SupportValid = 1u << 0;
    private const uint SupportGet = 1u << 1;
    private const uint SupportSet = 1u << 2;
    private const uint RequiredReadSupport = SupportValid | SupportGet;
    private const uint RequiredWriteSupport = SupportValid | SupportGet | SupportSet;

    private const uint FanDeviceId = 0x04;
    private const uint FanRpmFeatureId = 0x03;
    private const int FirstFanTypeId = 1;
    private const int MaximumFanChannels = 4;
    private const int RpmDivisor = 100;
    private const int MaximumPlausibleRpm = 20_000;

    private readonly object _gate = new();
    private readonly LenovoEnergyDrvFanProvider _energyDrv = new();
    private readonly bool _allowExactModelDirectIdFallback;
    private bool _discoveryComplete;
    private LenovoOtherModeFanChannel[] _channels = [];
    private LenovoOtherModeFanChannel[] _ownedChannels = [];
    private string _discoveryDetail = "Lenovo Other Mode fan capability not probed";

    internal LenovoOtherModeFanProvider(bool allowExactModelDirectIdFallback = false)
    {
        _allowExactModelDirectIdFallback = allowExactModelDirectIdFallback;
    }

    internal void Refresh()
    {
        lock (_gate)
        {
            _discoveryComplete = false;
            _channels = [];
            // Ownership is intentionally preserved through capability refresh. The
            // controller asks for Auto before refreshing, but if that handoff fails
            // we must retain the exact attribute IDs so a later cleanup can retry.
            _discoveryDetail = "Lenovo Other Mode fan capability not probed";
            _energyDrv.Refresh();
        }
    }

    internal LenovoOtherModeFanStatus ReadStatus()
    {
        LenovoOtherModeFanChannel[] channels;
        string detail;
        lock (_gate)
        {
            EnsureDiscoveredLocked();
            channels = _channels;
            detail = _discoveryDetail;
        }

        if (channels.Length == 0)
            return BuildReadOnlyEnergyDrvFallback(detail);

        try
        {
            using ManagementObject? method = FindActiveMethodObject();
            if (method is null)
                return BuildReadOnlyEnergyDrvFallback("LENOVO_OTHER_METHOD is unavailable");

            var fans = new List<LenovoFanReading>(channels.Length);
            var liveChannelIndexes = new HashSet<int>();
            foreach (LenovoOtherModeFanChannel channel in channels)
            {
                if (!CanReadChannel(channel))
                    continue;
                if (!TryGetFeatureValue(method, channel.AttributeId, out uint raw) || raw > MaximumPlausibleRpm)
                    continue;

                liveChannelIndexes.Add(channel.Index);
                fans.Add(new LenovoFanReading(
                    $"lenovo-other-mode-{channel.Index + 1}",
                    (int)raw,
                    $"Fan {channel.Index + 1}",
                    channel.CapabilityPresent
                        ? "Lenovo WMI · Other Mode direct target-RPM"
                        : "Lenovo WMI · Other Mode direct target-RPM · exact-X9 direct-ID fallback"));
            }

            LenovoOtherModeFanChannel[] writableLive = channels
                .Where(channel => liveChannelIndexes.Contains(channel.Index) && IsWritableChannel(channel))
                .ToArray();
            bool canControl = writableLive.Length >= 2;

            if (fans.Count == 0)
            {
                return BuildReadOnlyEnergyDrvFallback(
                    $"Lenovo Other Mode metadata found, but 0/{channels.Length} fan channels passed live GET validation · {DescribeChannelCapabilities(channels)}");
            }

            string liveSummary = $"{fans.Count}/{channels.Length} live · {writableLive.Length} live writable";
            string fallbackSummary = writableLive.Any(channel => !channel.CapabilityPresent)
                ? " · exact-X9 direct-ID fallback active because Capability Data omitted a live fan attribute"
                : string.Empty;
            return new LenovoOtherModeFanStatus(
                Available: true,
                CanControl: canControl,
                Fans: fans,
                Channels: channels,
                Detail: canControl
                    ? $"Lenovo Other Mode direct target-RPM · {liveSummary} · {DescribeRanges(writableLive)}{fallbackSummary}"
                    : $"Lenovo Other Mode fan telemetry · {liveSummary} · {detail} · {DescribeChannelCapabilities(channels)}");
        }
        catch (Exception ex)
        {
            return BuildReadOnlyEnergyDrvFallback(
                $"Lenovo Other Mode fan read failed: {DescribeManagementFailure(ex)}");
        }
    }

    internal bool SetPercent(int percent, out string? detail, out string? error)
    {
        detail = null;
        error = null;
        if (percent is < 0 or > 100)
        {
            error = "OEM fan target must be between 0% and 100%.";
            return false;
        }

        LenovoOtherModeFanChannel[] channels;
        lock (_gate)
        {
            EnsureDiscoveredLocked();
            channels = _channels;
        }

        LenovoOtherModeFanChannel[] candidates = channels.Where(IsWritableChannel).ToArray();
        if (candidates.Length < 2)
        {
            error = $"Lenovo Other Mode exposed only {candidates.Length} safe writable fan channel(s). Two independently live OEM target-RPM channels are required; Lenovo Auto keeps ownership.";
            return false;
        }

        LenovoOtherModeFanChannel[] liveWritable = [];
        var touched = new List<LenovoOtherModeFanChannel>(candidates.Length);
        try
        {
            using ManagementObject? method = FindActiveMethodObject();
            if (method is null)
            {
                error = "LENOVO_OTHER_METHOD is unavailable.";
                return false;
            }

            // Re-prove the read side immediately before taking ownership. Capability
            // metadata by itself is not sufficient permission for a hardware write;
            // the exact-X9 direct-ID fallback also requires this live read.
            liveWritable = candidates
                .Where(channel => TryGetFeatureValue(method, channel.AttributeId, out uint current) && current <= MaximumPlausibleRpm)
                .ToArray();

            if (liveWritable.Length < 2)
            {
                error = $"Only {liveWritable.Length}/{candidates.Length} safe OEM fan channels passed the live read gate. Lenovo Auto keeps ownership.";
                return false;
            }

            var targets = new List<string>(liveWritable.Length);
            foreach (LenovoOtherModeFanChannel channel in liveWritable)
            {
                int target = ResolveTargetRpm(channel, percent);
                touched.Add(channel);
                if (!TrySetFeatureValue(method, channel.AttributeId, (uint)target, out string? setError))
                {
                    BestEffortReturnToAuto(method, touched);
                    lock (_gate)
                        _ownedChannels = [];
                    error = $"Fan {channel.Index + 1} target-RPM write failed: {setError ?? "OEM method rejected the request"}. Lenovo Auto was requested for every touched channel.";
                    return false;
                }
                targets.Add($"Fan {channel.Index + 1} {target:N0} RPM");
            }

            lock (_gate)
                _ownedChannels = liveWritable.ToArray();

            string fallback = liveWritable.Any(channel => !channel.CapabilityPresent)
                ? " · exact-X9 direct-ID fallback"
                : string.Empty;
            detail = $"Lenovo OEM direct target-RPM · {string.Join(" · ", targets)}{fallback}";
            return true;
        }
        catch (Exception ex)
        {
            // Only channels that actually reached the write phase can require an
            // Auto recovery. Do not widen a failure cleanup to metadata-only peers.
            BestEffortRecoverAuto(touched);
            lock (_gate)
                _ownedChannels = [];
            error = $"Lenovo Other Mode fan write failed safely: {ex.Message}. Lenovo Auto was requested for every touched channel.";
            return false;
        }
    }

    internal bool ReturnToAuto(out string? error)
    {
        error = null;
        LenovoOtherModeFanChannel[] owned;
        lock (_gate)
            owned = _ownedChannels;

        if (owned.Length == 0)
            return true;

        try
        {
            using ManagementObject? method = FindActiveMethodObject();
            if (method is null)
            {
                error = "LENOVO_OTHER_METHOD is unavailable while returning owned fan targets to Auto.";
                return false;
            }

            bool ok = true;
            foreach (LenovoOtherModeFanChannel channel in owned)
                ok &= TrySetFeatureValue(method, channel.AttributeId, 0, out _);

            if (!ok)
            {
                error = "One or more ThinkControl-owned Lenovo fan channels did not accept the Auto target.";
                return false;
            }

            lock (_gate)
                _ownedChannels = [];
            return true;
        }
        catch (Exception ex)
        {
            error = $"Lenovo Other Mode Auto handoff failed: {ex.Message}";
            return false;
        }
    }

    private LenovoOtherModeFanStatus BuildReadOnlyEnergyDrvFallback(string otherModeDetail)
    {
        LenovoEnergyDrvFanStatus energy = _energyDrv.ReadStatus(DateTimeOffset.UtcNow);
        if (!energy.Available)
        {
            return new LenovoOtherModeFanStatus(
                false,
                false,
                [],
                [],
                $"{otherModeDetail} · {energy.Detail}");
        }

        string completeness = energy.Complete
            ? "two-channel OEM telemetry confirmed"
            : "partial OEM telemetry only";
        return new LenovoOtherModeFanStatus(
            true,
            false,
            energy.Fans,
            [],
            $"{otherModeDetail} · {energy.Detail} · {completeness} · EnergyDrv writer intentionally disabled pending exact X9 command validation");
    }

    private void EnsureDiscoveredLocked()
    {
        if (_discoveryComplete)
            return;

        _discoveryComplete = true;
        _channels = [];
        try
        {
            Dictionary<uint, uint> capabilities;
            string? capabilityFailure = null;
            try
            {
                capabilities = ReadCapabilities();
            }
            catch (Exception ex) when (_allowExactModelDirectIdFallback)
            {
                capabilities = [];
                capabilityFailure = DescribeManagementFailure(ex);
            }

            Dictionary<int, (int MinRpm, int MaxRpm)> constraints = ReadFanConstraints();
            var channels = new List<LenovoOtherModeFanChannel>(MaximumFanChannels);

            for (int index = 0; index < MaximumFanChannels; index++)
            {
                uint attributeId = BuildFanRpmAttributeId(index);
                bool capabilityPresent = capabilities.TryGetValue(attributeId, out uint capability);
                constraints.TryGetValue(index + FirstFanTypeId, out (int MinRpm, int MaxRpm) range);

                if (capabilityPresent)
                {
                    // Explicit firmware rejection always wins. The exact-model fallback
                    // only fills an omitted record; it never overrides an invalid one.
                    if ((capability & SupportValid) == 0)
                        continue;
                }
                else if (!_allowExactModelDirectIdFallback || !IsSaneConstraint(range.MinRpm, range.MaxRpm))
                {
                    continue;
                }

                channels.Add(new LenovoOtherModeFanChannel(
                    index,
                    attributeId,
                    range.MinRpm,
                    range.MaxRpm,
                    capability,
                    capabilityPresent));
            }

            _channels = channels.ToArray();
            if (_channels.Length == 0)
            {
                string capDetail = capabilityFailure is null
                    ? $"capdata records {capabilities.Count}"
                    : $"capdata unavailable ({capabilityFailure})";
                _discoveryDetail = $"Lenovo Other Mode exposes no safe fan-RPM candidates (checked 0x{BuildFanRpmAttributeId(0):X8}..0x{BuildFanRpmAttributeId(MaximumFanChannels - 1):X8}; {capDetail})";
                return;
            }

            int writableCandidates = _channels.Count(IsWritableChannel);
            int directIdCandidates = _channels.Count(channel => !channel.CapabilityPresent);
            string directIdDetail = directIdCandidates > 0
                ? $" · {directIdCandidates} exact-X9 direct-ID candidate(s) from Fan Test Data"
                : string.Empty;
            string capFailureDetail = capabilityFailure is null ? string.Empty : $" · capdata query failed: {capabilityFailure}";
            _discoveryDetail = writableCandidates >= 2
                ? $"Lenovo Other Mode direct target-RPM candidates found · {writableCandidates}/{_channels.Length} safe writable · live GET still required{directIdDetail}{capFailureDetail}"
                : $"Lenovo Other Mode fan metadata found · {writableCandidates}/{_channels.Length} safe writable · {DescribeChannelCapabilities(_channels)}{capFailureDetail}";
        }
        catch (Exception ex)
        {
            _channels = [];
            _discoveryDetail = $"Lenovo Other Mode capability discovery failed: {DescribeManagementFailure(ex)}";
        }
    }

    private static Dictionary<uint, uint> ReadCapabilities()
    {
        var result = new Dictionary<uint, uint>();
        using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {CapabilityClass}");
        using ManagementObjectCollection collection = searcher.Get();
        foreach (ManagementObject item in collection)
        {
            using (item)
            {
                if (!TryUInt32(item["IDs"], out uint id) || !TryUInt32(item["Capability"], out uint capability))
                    continue;
                result[id] = capability;
            }
        }
        return result;
    }

    private static Dictionary<int, (int MinRpm, int MaxRpm)> ReadFanConstraints()
    {
        var result = new Dictionary<int, (int MinRpm, int MaxRpm)>();
        using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {FanTestClass}");
        using ManagementObjectCollection collection = searcher.Get();
        foreach (ManagementObject item in collection)
        {
            using (item)
            {
                uint[] ids = ToUInt32Array(item["FanId"]);
                uint[] mins = ToUInt32Array(item["FanMinSpeed"]);
                uint[] maxes = ToUInt32Array(item["FanMaxSpeed"]);
                int count = Math.Min(ids.Length, Math.Min(mins.Length, maxes.Length));
                for (int i = 0; i < count; i++)
                {
                    if (ids[i] is < 1 or > MaximumFanChannels || mins[i] > int.MaxValue || maxes[i] > int.MaxValue)
                        continue;
                    result[(int)ids[i]] = ((int)mins[i], (int)maxes[i]);
                }
            }
            if (result.Count > 0)
                break;
        }
        return result;
    }

    private static ManagementObject? FindActiveMethodObject()
    {
        using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {MethodClass}");
        using ManagementObjectCollection collection = searcher.Get();
        foreach (ManagementObject item in collection)
        {
            bool active = item["Active"] is not bool value || value;
            if (active)
                return item;
            item.Dispose();
        }
        return null;
    }

    private static bool TryGetFeatureValue(ManagementObject method, uint attributeId, out uint value)
    {
        value = 0;
        try
        {
            using ManagementBaseObject input = method.GetMethodParameters("GetFeatureValue");
            input["IDs"] = attributeId;
            using ManagementBaseObject? output = method.InvokeMethod("GetFeatureValue", input, null);
            return output is not null && TryUInt32(output["value"], out value);
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetFeatureValue(ManagementObject method, uint attributeId, uint value, out string? error)
    {
        error = null;
        try
        {
            using ManagementBaseObject input = method.GetMethodParameters("SetFeatureValue");
            input["IDs"] = attributeId;
            input["value"] = value;
            using ManagementBaseObject? output = method.InvokeMethod("SetFeatureValue", input, null);

            // Lenovo's decoded MOF declares SetFeatureValue as void. Successful
            // System.Management invocation is the Windows-side acknowledgement;
            // no invented ReturnValue rule is applied where WMI does not expose one.
            _ = output;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name;
            return false;
        }
    }

    private void BestEffortReturnToAuto(ManagementObject method, IEnumerable<LenovoOtherModeFanChannel> channels)
    {
        foreach (LenovoOtherModeFanChannel channel in channels)
        {
            if (!IsWritableChannel(channel))
                continue;
            try { TrySetFeatureValue(method, channel.AttributeId, 0, out _); }
            catch { }
        }
    }

    private void BestEffortRecoverAuto(IReadOnlyList<LenovoOtherModeFanChannel> channels)
    {
        if (channels.Count == 0)
            return;

        try
        {
            using ManagementObject? method = FindActiveMethodObject();
            if (method is not null)
                BestEffortReturnToAuto(method, channels);
        }
        catch
        {
        }
    }

    private bool CanReadChannel(LenovoOtherModeFanChannel channel)
    {
        if (channel.CapabilityPresent)
            return (channel.Capability & RequiredReadSupport) == RequiredReadSupport;
        return _allowExactModelDirectIdFallback && IsSaneConstraint(channel.MinRpm, channel.MaxRpm);
    }

    private bool IsWritableChannel(LenovoOtherModeFanChannel channel)
    {
        if (!IsSaneConstraint(channel.MinRpm, channel.MaxRpm))
            return false;
        if (channel.CapabilityPresent)
            return (channel.Capability & RequiredWriteSupport) == RequiredWriteSupport;
        return _allowExactModelDirectIdFallback;
    }

    private static string DescribeRanges(IEnumerable<LenovoOtherModeFanChannel> channels) =>
        string.Join(" · ", channels.Select(channel =>
            $"Fan {channel.Index + 1} {channel.MinRpm:N0}–{channel.MaxRpm:N0} RPM"));

    private static string DescribeChannelCapabilities(IEnumerable<LenovoOtherModeFanChannel> channels) =>
        string.Join(" · ", channels.Select(channel =>
        {
            string capability = channel.CapabilityPresent
                ? $"cap=0x{channel.Capability:X}({(((channel.Capability & SupportValid) != 0) ? "V" : "-")}{(((channel.Capability & SupportGet) != 0) ? "R" : "-")}{(((channel.Capability & SupportSet) != 0) ? "W" : "-")})"
                : "cap=missing(exact-X9-direct-ID)";
            string range = IsSaneConstraint(channel.MinRpm, channel.MaxRpm)
                ? $"{channel.MinRpm}-{channel.MaxRpm}RPM"
                : "no-safe-range";
            return $"Fan {channel.Index + 1} id=0x{channel.AttributeId:X8} {capability} {range}";
        }));

    private static string DescribeManagementFailure(Exception ex)
    {
        if (ex is ManagementException management)
            return $"{ex.GetType().Name}/{management.ErrorCode}";
        return ex.GetType().Name;
    }

    private static int ResolveTargetRpm(LenovoOtherModeFanChannel channel, int percent)
    {
        double span = channel.MaxRpm - channel.MinRpm;
        int target = channel.MinRpm + (int)Math.Round(span * Math.Clamp(percent, 0, 100) / 100.0);
        target = Math.Clamp(target, channel.MinRpm, channel.MaxRpm);
        target = target / RpmDivisor * RpmDivisor;
        return Math.Max(channel.MinRpm, target);
    }

    private static uint BuildFanRpmAttributeId(int zeroBasedIndex)
    {
        uint typeId = (uint)(zeroBasedIndex + FirstFanTypeId);
        return FanDeviceId << 24 | FanRpmFeatureId << 16 | typeId;
    }

    private static bool IsSaneConstraint(int minRpm, int maxRpm) =>
        minRpm >= RpmDivisor && maxRpm > minRpm && maxRpm <= MaximumPlausibleRpm;

    private static bool TryUInt32(object? value, out uint result)
    {
        result = 0;
        try
        {
            if (value is null)
                return false;
            result = Convert.ToUInt32(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static uint[] ToUInt32Array(object? value)
    {
        if (value is null)
            return [];
        if (value is uint[] typed)
            return typed;
        if (value is not IEnumerable enumerable)
            return [];

        var result = new List<uint>();
        foreach (object? item in enumerable)
        {
            if (TryUInt32(item, out uint parsed))
                result.Add(parsed);
        }
        return result.ToArray();
    }
}
