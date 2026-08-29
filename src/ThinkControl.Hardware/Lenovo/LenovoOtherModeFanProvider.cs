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
    uint Capability);

internal sealed record LenovoOtherModeFanStatus(
    bool Available,
    bool CanControl,
    IReadOnlyList<LenovoFanReading> Fans,
    IReadOnlyList<LenovoOtherModeFanChannel> Channels,
    string Detail);

/// <summary>
/// Lenovo OEM fan provider coordinator.
///
/// The preferred writable transport is Lenovo's modern "Other Mode" WMI
/// contract, which exposes semantic target RPM plus capability metadata and
/// per-fan constraints. Some Lenovo families (including the X9 under current
/// investigation) may not expose those WMI classes even though Lenovo's own
/// Windows stack still exposes fan telemetry through EnergyDrv. In that case
/// EnergyDrv is used read-only so ThinkControl can stop touching EC tachometers
/// just to observe fan speed while the exact OEM writer is recovered.
///
/// EnergyDrv writes are intentionally absent here: public Lenovo code proves a
/// separate ChangeFanSpeed IOCTL exists, but its command encoding is not yet
/// physically validated on the X9. Missing write semantics must fail closed.
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
    private bool _discoveryComplete;
    private LenovoOtherModeFanChannel[] _channels = [];
    private string _discoveryDetail = "Lenovo Other Mode fan capability not probed";

    internal void Refresh()
    {
        lock (_gate)
        {
            _discoveryComplete = false;
            _channels = [];
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
            foreach (LenovoOtherModeFanChannel channel in channels)
            {
                if ((channel.Capability & RequiredReadSupport) != RequiredReadSupport)
                    continue;
                if (!TryGetFeatureValue(method, channel.AttributeId, out uint raw) || raw > MaximumPlausibleRpm)
                    continue;

                fans.Add(new LenovoFanReading(
                    $"lenovo-other-mode-{channel.Index + 1}",
                    (int)raw,
                    $"Fan {channel.Index + 1}",
                    "Lenovo WMI · Other Mode target-RPM provider"));
            }

            bool canControl = channels.Length >= 2 &&
                              fans.Count == channels.Length &&
                              channels.All(channel =>
                                  (channel.Capability & RequiredWriteSupport) == RequiredWriteSupport &&
                                  IsSaneConstraint(channel.MinRpm, channel.MaxRpm));
            if (fans.Count == 0)
            {
                return BuildReadOnlyEnergyDrvFallback(
                    $"Lenovo Other Mode metadata found, but 0/{channels.Length} fan channels passed live GET validation");
            }

            return new LenovoOtherModeFanStatus(
                Available: true,
                CanControl: canControl,
                Fans: fans,
                Channels: channels,
                Detail: canControl
                    ? $"Lenovo Other Mode · {DescribeRanges(channels)}"
                    : fans.Count != channels.Length
                        ? $"Lenovo Other Mode metadata found, but only {fans.Count}/{channels.Length} fan channels passed live GET validation"
                        : detail);
        }
        catch (Exception ex)
        {
            return BuildReadOnlyEnergyDrvFallback(
                $"Lenovo Other Mode fan read failed: {ex.GetType().Name}");
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

        if (channels.Length < 2 || channels.Any(channel =>
                (channel.Capability & RequiredWriteSupport) != RequiredWriteSupport ||
                !IsSaneConstraint(channel.MinRpm, channel.MaxRpm)))
        {
            error = "Lenovo Other Mode did not expose two fully constrained writable fan channels. EnergyDrv is telemetry-only until the exact X9 ChangeFanSpeed command is recovered.";
            return false;
        }

        try
        {
            using ManagementObject? method = FindActiveMethodObject();
            if (method is null)
            {
                error = "LENOVO_OTHER_METHOD is unavailable.";
                return false;
            }

            // Re-prove the read side immediately before taking ownership. Capability
            // metadata by itself is not sufficient permission for a hardware write.
            foreach (LenovoOtherModeFanChannel channel in channels)
            {
                if (!TryGetFeatureValue(method, channel.AttributeId, out uint current) || current > MaximumPlausibleRpm)
                {
                    error = $"Fan {channel.Index + 1} failed the live OEM read gate; Lenovo Auto keeps ownership.";
                    return false;
                }
            }

            var targets = new List<string>(channels.Length);
            foreach (LenovoOtherModeFanChannel channel in channels)
            {
                int target = ResolveTargetRpm(channel, percent);
                if (!TrySetFeatureValue(method, channel.AttributeId, (uint)target, out string? setError))
                {
                    BestEffortReturnToAuto(method, channels);
                    error = $"Fan {channel.Index + 1} target-RPM write failed: {setError ?? "OEM method rejected the request"}. Lenovo Auto was requested for all channels.";
                    return false;
                }
                targets.Add($"Fan {channel.Index + 1} {target:N0} RPM");
            }

            detail = $"Lenovo OEM target-RPM · {string.Join(" · ", targets)}";
            return true;
        }
        catch (Exception ex)
        {
            // An exception can occur after an earlier channel was accepted. Re-open
            // the documented method surface and request Auto for every writable fan
            // so a partial multi-fan target cannot survive an unexpected WMI failure.
            BestEffortRecoverAuto(channels);
            error = $"Lenovo Other Mode fan write failed safely: {ex.Message}. Lenovo Auto was requested for all writable channels.";
            return false;
        }
    }

    internal bool ReturnToAuto(out string? error)
    {
        error = null;
        LenovoOtherModeFanChannel[] channels;
        lock (_gate)
        {
            EnsureDiscoveredLocked();
            channels = _channels;
        }

        if (channels.Length == 0)
        {
            // EnergyDrv has no validated write/handoff contract in ThinkControl yet.
            // With no Other Mode writer active there is nothing for this provider to
            // release; firmware already owns the EnergyDrv-observed fans.
            error = null;
            return true;
        }

        try
        {
            using ManagementObject? method = FindActiveMethodObject();
            if (method is null)
            {
                error = "LENOVO_OTHER_METHOD is unavailable.";
                return false;
            }

            bool ok = true;
            foreach (LenovoOtherModeFanChannel channel in channels)
            {
                if ((channel.Capability & RequiredWriteSupport) != RequiredWriteSupport)
                    continue;
                ok &= TrySetFeatureValue(method, channel.AttributeId, 0, out _);
            }

            if (!ok)
                error = "One or more Lenovo fan channels did not accept the Auto target.";
            return ok;
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
            Dictionary<uint, uint> capabilities = ReadCapabilities();
            Dictionary<int, (int MinRpm, int MaxRpm)> constraints = ReadFanConstraints();
            var channels = new List<LenovoOtherModeFanChannel>(MaximumFanChannels);

            for (int index = 0; index < MaximumFanChannels; index++)
            {
                uint attributeId = BuildFanRpmAttributeId(index);
                if (!capabilities.TryGetValue(attributeId, out uint capability) ||
                    (capability & SupportValid) == 0)
                    continue;

                constraints.TryGetValue(index + FirstFanTypeId, out (int MinRpm, int MaxRpm) range);
                channels.Add(new LenovoOtherModeFanChannel(
                    index,
                    attributeId,
                    range.MinRpm,
                    range.MaxRpm,
                    capability));
            }

            _channels = channels.ToArray();
            if (_channels.Length == 0)
            {
                _discoveryDetail = "Lenovo Other Mode exposes no valid fan-RPM capabilities";
                return;
            }

            int writable = _channels.Count(channel =>
                (channel.Capability & RequiredWriteSupport) == RequiredWriteSupport &&
                IsSaneConstraint(channel.MinRpm, channel.MaxRpm));
            _discoveryDetail = writable == _channels.Length && writable >= 2
                ? $"Lenovo Other Mode target-RPM metadata ready · {DescribeRanges(_channels)} · live GET still required"
                : $"Lenovo Other Mode fan telemetry found · {writable}/{_channels.Length} channels have OEM write constraints";
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

            // The decoded Lenovo MOF declares SetFeatureValue as void. Successful
            // WMI invocation is therefore the protocol acknowledgement; no invented
            // ReturnValue==0 rule is applied here.
            _ = output;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name;
            return false;
        }
    }

    private static void BestEffortReturnToAuto(ManagementObject method, IEnumerable<LenovoOtherModeFanChannel> channels)
    {
        foreach (LenovoOtherModeFanChannel channel in channels)
        {
            if ((channel.Capability & RequiredWriteSupport) != RequiredWriteSupport)
                continue;
            try { TrySetFeatureValue(method, channel.AttributeId, 0, out _); }
            catch { }
        }
    }

    private static void BestEffortRecoverAuto(IReadOnlyList<LenovoOtherModeFanChannel> channels)
    {
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

    private static string DescribeRanges(IEnumerable<LenovoOtherModeFanChannel> channels) =>
        string.Join(" · ", channels.Select(channel =>
            $"Fan {channel.Index + 1} {channel.MinRpm:N0}–{channel.MaxRpm:N0} RPM"));

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
