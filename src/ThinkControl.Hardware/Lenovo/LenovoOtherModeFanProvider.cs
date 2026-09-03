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
/// Canonical discovery uses Capability Data 00 plus Fan Test Data. The provider
/// can additionally use a narrow direct-ID fallback when Capability Data omits a
/// fan attribute: the known 0x0403000N ID must still answer a live GetFeatureValue
/// and that physical fan must have a sane Fan Test RPM range. An explicitly
/// present but invalid/readonly capability is never overridden. ThinkControl's
/// parent hardware controller still gates all writes to the exact verified X9.
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
    private const int TargetVerificationToleranceRpm = 200;
    private const int TargetVerificationMinimumMovementRpm = 100;
    private const int TargetVerificationDelayMs = 450;
    private const int TargetVerificationAttempts = 2;
    private static readonly TimeSpan LiveProbeFailureBackoff = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private readonly LenovoEnergyDrvFanProvider _energyDrv = new();
    private readonly bool _allowExactModelDirectIdFallback;
    private bool _discoveryComplete;
    private LenovoOtherModeFanChannel[] _channels = [];
    private LenovoOtherModeFanChannel[] _ownedChannels = [];
    private string _discoveryDetail = "Lenovo Other Mode fan capability not probed";
    private DateTimeOffset _liveProbeRetryAfter = DateTimeOffset.MinValue;
    private string? _lastLiveProbeFailure;

    // Only LenovoHardwareController owns this provider in production, and that
    // controller performs the exact 21Q6/21Q7 identity gate before any SetPercent
    // call. Keeping the read-side direct-ID probe enabled lets incomplete Lenovo
    // capdata be diagnosed without weakening the external write boundary.
    internal LenovoOtherModeFanProvider(bool allowExactModelDirectIdFallback = true)
    {
        _allowExactModelDirectIdFallback = allowExactModelDirectIdFallback;
    }

    internal void Refresh()
    {
        lock (_gate)
        {
            _discoveryComplete = false;
            _channels = [];
            _liveProbeRetryAfter = DateTimeOffset.MinValue;
            _lastLiveProbeFailure = null;
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

        if (ShouldBackOffLiveProbe(out string? backoffDetail))
            return BuildReadOnlyEnergyDrvFallback($"{detail} · {backoffDetail}");

        try
        {
            using ManagementObject? method = FindActiveMethodObject();
            if (method is null)
            {
                RecordLiveProbeFailure("LENOVO_OTHER_METHOD is unavailable");
                return BuildReadOnlyEnergyDrvFallback("LENOVO_OTHER_METHOD is unavailable");
            }

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
                string failure = $"0/{channels.Length} fan channels passed live GET validation";
                RecordLiveProbeFailure(failure);
                return BuildReadOnlyEnergyDrvFallback(
                    $"Lenovo Other Mode metadata found, but {failure} · {DescribeChannelCapabilities(channels)}");
            }

            ClearLiveProbeFailure();
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
            string failure = $"Lenovo Other Mode fan read failed: {DescribeManagementFailure(ex)}";
            RecordLiveProbeFailure(failure);
            return BuildReadOnlyEnergyDrvFallback(failure);
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
            var before = new Dictionary<int, uint>();
            LenovoOtherModeFanChannel[] liveWritable = candidates
                .Where(channel => TryCaptureLiveRpm(method, channel, before))
                .ToArray();

            if (liveWritable.Length < 2)
            {
                error = $"Only {liveWritable.Length}/{candidates.Length} safe OEM fan channels passed the live read gate. Lenovo Auto keeps ownership.";
                return false;
            }

            var targets = new Dictionary<int, int>();
            foreach (LenovoOtherModeFanChannel channel in liveWritable)
            {
                int target = ResolveTargetRpm(channel, percent);
                targets[channel.Index] = target;
                touched.Add(channel);
                if (!TrySetFeatureValue(method, channel.AttributeId, (uint)target, out string? setError))
                {
                    LenovoOtherModeFanChannel[] failedAuto = ReturnChannelsToAuto(method, touched);
                    PreserveFailedAutoOwnership(failedAuto);
                    error = $"Fan {channel.Index + 1} target-RPM write failed: {setError ?? "OEM method rejected the request"}. " +
                            AutoRecoveryText(failedAuto);
                    return false;
                }
            }

            // The WMI MOF declares SetFeatureValue as void, so a non-throwing call is
            // not enough evidence that firmware accepted a target. Verify the real
            // fanX_input response after one bounded settle window. We only require
            // meaningful movement toward the target (or already-near-target state),
            // not instantaneous equality while the fan is still ramping.
            if (!VerifyTargetResponse(method, liveWritable, before, targets, out string verificationError))
            {
                LenovoOtherModeFanChannel[] failedAuto = ReturnChannelsToAuto(method, touched);
                PreserveFailedAutoOwnership(failedAuto);
                error = $"Lenovo OEM target did not verify from live RPM response: {verificationError}. {AutoRecoveryText(failedAuto)}";
                return false;
            }

            lock (_gate)
                _ownedChannels = liveWritable.ToArray();

            string fallback = liveWritable.Any(channel => !channel.CapabilityPresent)
                ? " · exact-X9 direct-ID fallback"
                : string.Empty;
            string targetText = string.Join(" · ", liveWritable.Select(channel =>
                $"Fan {channel.Index + 1} {targets[channel.Index]:N0} RPM"));
            detail = $"Lenovo OEM direct target-RPM · {targetText} · live response verified{fallback}";
            return true;
        }
        catch (Exception ex)
        {
            // Only channels that actually reached the write phase can require an
            // Auto recovery. Do not widen a failure cleanup to metadata-only peers.
            LenovoOtherModeFanChannel[] failedAuto = RecoverAuto(touched);
            PreserveFailedAutoOwnership(failedAuto);
            error = $"Lenovo Other Mode fan write failed safely: {ex.Message}. {AutoRecoveryText(failedAuto)}";
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

            LenovoOtherModeFanChannel[] failed = ReturnChannelsToAuto(method, owned);
            lock (_gate)
                _ownedChannels = failed;
            if (failed.Length > 0)
            {
                error = "One or more ThinkControl-owned Lenovo fan channels did not accept the Auto target; failed ownership was retained for a later cleanup retry.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Lenovo Other Mode Auto handoff failed: {ex.Message}";
            return false;
        }
    }

    // This is deliberately different from ReturnToAuto(). ReturnToAuto() is used
    // by automatic cleanup and touches only channels this provider knows it owns.
    // RequestFirmwareAuto() is for an explicit user/safety request to reassert
    // Lenovo firmware ownership even after a UI/service restart lost that in-memory
    // ownership record. It still requires two independently live safe channels.
    internal bool RequestFirmwareAuto(out string? detail, out string? error)
    {
        detail = null;
        error = null;

        LenovoOtherModeFanChannel[] channels;
        lock (_gate)
        {
            EnsureDiscoveredLocked();
            channels = _channels;
        }

        LenovoOtherModeFanChannel[] candidates = channels.Where(IsWritableChannel).ToArray();
        if (candidates.Length < 2)
        {
            error = $"Lenovo Other Mode exposed only {candidates.Length} safe writable fan channel(s); direct Auto reassertion needs two live channels.";
            return false;
        }

        try
        {
            using ManagementObject? method = FindActiveMethodObject();
            if (method is null)
            {
                error = "LENOVO_OTHER_METHOD is unavailable while reasserting Lenovo Auto.";
                return false;
            }

            LenovoOtherModeFanChannel[] liveWritable = candidates
                .Where(channel => TryGetFeatureValue(method, channel.AttributeId, out uint current) && current <= MaximumPlausibleRpm)
                .ToArray();
            if (liveWritable.Length < 2)
            {
                error = $"Only {liveWritable.Length}/{candidates.Length} OEM fan channels passed the live read gate; Lenovo Auto could not be reasserted safely.";
                return false;
            }

            LenovoOtherModeFanChannel[] failed = ReturnChannelsToAuto(method, liveWritable);
            if (failed.Length > 0)
            {
                PreserveFailedAutoOwnership(failed);
                error = "Lenovo Auto reassertion failed on " + string.Join(" · ", failed.Select(channel => $"Fan {channel.Index + 1}")) +
                        "; failed channels were retained as owned so cleanup can retry.";
                return false;
            }

            lock (_gate)
                _ownedChannels = [];
            detail = $"Lenovo Auto reasserted through OEM target 0 on {liveWritable.Length} live fan channels";
            return true;
        }
        catch (Exception ex)
        {
            error = $"Lenovo Other Mode Auto reassertion failed: {ex.Message}";
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
                    // Explicit firmware rejection always wins. The direct-ID fallback
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
                ? $" · {directIdCandidates} direct-ID candidate(s) from Fan Test Data"
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

            // The decoded MOF declares SetFeatureValue as void, but some firmware/
            // WMI providers still surface a ReturnValue. When present, Lenovo's
            // upstream implementation accepts 0 (no error) and 1 (done).
            if (output?.Properties["ReturnValue"]?.Value is object statusValue &&
                TryUInt32(statusValue, out uint status) && status is not 0 and not 1)
            {
                error = $"OEM method returned status {status}.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name;
            return false;
        }
    }

    private static bool TryCaptureLiveRpm(
        ManagementObject method,
        LenovoOtherModeFanChannel channel,
        IDictionary<int, uint> values)
    {
        if (!TryGetFeatureValue(method, channel.AttributeId, out uint current) || current > MaximumPlausibleRpm)
            return false;
        values[channel.Index] = current;
        return true;
    }

    private static bool VerifyTargetResponse(
        ManagementObject method,
        IReadOnlyList<LenovoOtherModeFanChannel> channels,
        IReadOnlyDictionary<int, uint> before,
        IReadOnlyDictionary<int, int> targets,
        out string detail)
    {
        detail = string.Empty;
        var pending = new HashSet<int>(channels.Select(channel => channel.Index));
        var last = new Dictionary<int, uint>();

        for (int attempt = 0; attempt < TargetVerificationAttempts && pending.Count > 0; attempt++)
        {
            Thread.Sleep(TargetVerificationDelayMs);
            foreach (LenovoOtherModeFanChannel channel in channels.Where(channel => pending.Contains(channel.Index)))
            {
                if (!TryGetFeatureValue(method, channel.AttributeId, out uint current) || current > MaximumPlausibleRpm)
                    continue;
                last[channel.Index] = current;
                if (HasVerifiedTargetProgress(before[channel.Index], targets[channel.Index], current))
                    pending.Remove(channel.Index);
            }
        }

        if (pending.Count == 0)
            return true;

        detail = string.Join(" · ", pending.Select(index =>
        {
            uint original = before[index];
            int target = targets[index];
            string observed = last.TryGetValue(index, out uint current) ? current.ToString("N0") : "unavailable";
            return $"Fan {index + 1} before {original:N0}, target {target:N0}, observed {observed} RPM";
        }));
        return false;
    }

    private static bool HasVerifiedTargetProgress(uint before, int target, uint current)
    {
        int start = (int)before;
        int observed = (int)current;
        if (Math.Abs(start - target) <= TargetVerificationToleranceRpm)
            return Math.Abs(observed - target) <= TargetVerificationToleranceRpm;
        if (Math.Abs(observed - target) <= TargetVerificationToleranceRpm)
            return true;
        if (target > start)
            return observed >= start + TargetVerificationMinimumMovementRpm;
        return observed <= start - TargetVerificationMinimumMovementRpm;
    }

    private static LenovoOtherModeFanChannel[] ReturnChannelsToAuto(
        ManagementObject method,
        IEnumerable<LenovoOtherModeFanChannel> channels)
    {
        var failed = new List<LenovoOtherModeFanChannel>();
        foreach (LenovoOtherModeFanChannel channel in channels.DistinctBy(channel => channel.Index))
        {
            try
            {
                if (!TrySetFeatureValue(method, channel.AttributeId, 0, out _))
                    failed.Add(channel);
            }
            catch
            {
                failed.Add(channel);
            }
        }
        return failed.ToArray();
    }

    private static LenovoOtherModeFanChannel[] RecoverAuto(IReadOnlyList<LenovoOtherModeFanChannel> channels)
    {
        if (channels.Count == 0)
            return [];

        try
        {
            using ManagementObject? method = FindActiveMethodObject();
            return method is null ? channels.ToArray() : ReturnChannelsToAuto(method, channels);
        }
        catch
        {
            return channels.ToArray();
        }
    }

    private void PreserveFailedAutoOwnership(IReadOnlyList<LenovoOtherModeFanChannel> failed)
    {
        lock (_gate)
            _ownedChannels = failed.DistinctBy(channel => channel.Index).ToArray();
    }

    private static string AutoRecoveryText(IReadOnlyList<LenovoOtherModeFanChannel> failed) =>
        failed.Count == 0
            ? "Lenovo Auto was verified for every touched channel."
            : $"Auto recovery did not verify for {failed.Count} touched channel(s); ownership was retained so cleanup can retry.";

    private bool ShouldBackOffLiveProbe(out string? detail)
    {
        lock (_gate)
        {
            if (DateTimeOffset.UtcNow >= _liveProbeRetryAfter)
            {
                detail = null;
                return false;
            }

            int seconds = Math.Max(1, (int)Math.Ceiling((_liveProbeRetryAfter - DateTimeOffset.UtcNow).TotalSeconds));
            detail = $"Other Mode live probe backoff ({_lastLiveProbeFailure ?? "previous failure"}; retry in ~{seconds}s)";
            return true;
        }
    }

    private void RecordLiveProbeFailure(string detail)
    {
        lock (_gate)
        {
            _lastLiveProbeFailure = detail;
            _liveProbeRetryAfter = DateTimeOffset.UtcNow + LiveProbeFailureBackoff;
        }
    }

    private void ClearLiveProbeFailure()
    {
        lock (_gate)
        {
            _lastLiveProbeFailure = null;
            _liveProbeRetryAfter = DateTimeOffset.MinValue;
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
                : "cap=missing(direct-ID)";
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
