using System.Management;
using ThinkControl.Hardware.X9;

namespace ThinkControl.Hardware.Lenovo;

internal sealed record LenovoOtherModeFullSpeedStatus(
    bool Available,
    bool Writable,
    bool Enabled,
    bool CapabilityPresent,
    uint Capability,
    string Detail);

/// <summary>
/// Exact-X9 bridge for Lenovo Other Mode's documented global full-speed semantic.
///
/// Upstream Lenovo/Linux support and independent modern Lenovo tooling use feature
/// 0x04020000 as a boolean full-speed override: 1 requests full speed and 0 releases
/// it back to firmware. ThinkControl does not infer this from a fan-test RPM ceiling
/// and does not reuse the rejected fanX_target writer.
///
/// Product writes remain exact-X9 and live-read gated. An explicitly present
/// capability row must advertise VALID+GET+SET. If Lenovo omits the capability row,
/// the known feature ID is accepted only when GetFeatureValue itself returns a real
/// boolean on the exact X9 immediately before the write. Every write is verified by
/// readback. No arbitrary IDs or values are accepted.
/// </summary>
internal static class LenovoOtherModeFullSpeedService
{
    private const string WmiNamespace = @"root\WMI";
    private const string MethodClass = "LENOVO_OTHER_METHOD";
    private const string CapabilityClass = "LENOVO_CAPABILITY_DATA_00";
    private const uint FullSpeedAttributeId = 0x04020000;
    private const uint SupportValid = 1u << 0;
    private const uint SupportGet = 1u << 1;
    private const uint SupportSet = 1u << 2;
    private const uint RequiredWriteSupport = SupportValid | SupportGet | SupportSet;

    internal static LenovoOtherModeFullSpeedStatus Read(HardwareDeviceIdentity identity)
    {
        if (!identity.IsVerifiedX9)
        {
            return new LenovoOtherModeFullSpeedStatus(
                false, false, false, false, 0,
                "Lenovo Other Mode full-speed probing is restricted to the verified X9 21Q6/21Q7 profile.");
        }

        try
        {
            (bool capabilityPresent, uint capability) = ReadCapability();
            if (capabilityPresent && (capability & (SupportValid | SupportGet)) != (SupportValid | SupportGet))
            {
                return new LenovoOtherModeFullSpeedStatus(
                    false, false, false, true, capability,
                    $"X9 firmware explicitly exposes 0x{FullSpeedAttributeId:X8} without a valid readable contract (cap=0x{capability:X}).");
            }

            using ManagementObject? method = FindActiveMethodObject();
            if (method is null)
            {
                return new LenovoOtherModeFullSpeedStatus(
                    false, false, false, capabilityPresent, capability,
                    "LENOVO_OTHER_METHOD is unavailable while probing the X9 full-speed feature.");
            }

            if (!TryGetFeatureValue(method, FullSpeedAttributeId, out uint raw) || raw > 1)
            {
                return new LenovoOtherModeFullSpeedStatus(
                    false, false, false, capabilityPresent, capability,
                    $"X9 full-speed feature 0x{FullSpeedAttributeId:X8} did not return a boolean live value.");
            }

            bool writable = capabilityPresent
                ? (capability & RequiredWriteSupport) == RequiredWriteSupport
                : true;
            string contract = capabilityPresent
                ? $"cap=0x{capability:X}"
                : "live direct-ID fallback; capability row omitted";
            return new LenovoOtherModeFullSpeedStatus(
                true,
                writable,
                raw == 1,
                capabilityPresent,
                capability,
                $"Lenovo Other Mode full-speed 0x{FullSpeedAttributeId:X8} = {raw} · {contract}");
        }
        catch (Exception ex)
        {
            return new LenovoOtherModeFullSpeedStatus(
                false, false, false, false, 0,
                $"X9 full-speed probe failed safely: {DescribeManagementFailure(ex)}");
        }
    }

    internal static bool TrySet(
        HardwareDeviceIdentity identity,
        bool enabled,
        out bool changed,
        out string? detail)
    {
        changed = false;
        detail = null;

        LenovoOtherModeFullSpeedStatus status = Read(identity);
        if (!status.Available)
        {
            detail = status.Detail;
            return false;
        }
        if (!status.Writable)
        {
            detail = $"{status.Detail} · firmware does not advertise a safe write contract.";
            return false;
        }
        if (status.Enabled == enabled)
        {
            detail = $"{status.Detail} · requested state was already active; ThinkControl did not take ownership.";
            return true;
        }

        try
        {
            using ManagementObject? method = FindActiveMethodObject();
            if (method is null)
            {
                detail = "LENOVO_OTHER_METHOD disappeared before the full-speed transition.";
                return false;
            }

            // Re-prove the exact feature immediately before writing. An omitted
            // Capability Data row is tolerated only because this exact known feature
            // returned a boolean value on the verified X9 in the same transaction.
            if (!TryGetFeatureValue(method, FullSpeedAttributeId, out uint before) || before > 1)
            {
                detail = "The X9 full-speed feature no longer returns a safe boolean live value.";
                return false;
            }
            if (before == (enabled ? 1u : 0u))
            {
                detail = $"Lenovo Other Mode full-speed was already {(enabled ? "enabled" : "disabled")}; ThinkControl did not take ownership.";
                return true;
            }

            if (!TrySetFeatureValue(method, FullSpeedAttributeId, enabled ? 1u : 0u, out string? setError))
            {
                detail = setError ?? "Lenovo Other Mode rejected the full-speed transition.";
                return false;
            }
            if (!TryGetFeatureValue(method, FullSpeedAttributeId, out uint verified) ||
                verified != (enabled ? 1u : 0u))
            {
                // Best-effort rollback only when this call was enabling the feature.
                // A failed disable never attempts to turn it back on.
                if (enabled)
                    _ = TrySetFeatureValue(method, FullSpeedAttributeId, 0u, out _);
                detail = $"Lenovo full-speed readback did not verify {(enabled ? 1 : 0)}; firmware ownership was requested as the safe fallback.";
                return false;
            }

            changed = true;
            detail = $"Lenovo Other Mode full-speed {(enabled ? "enabled" : "disabled")} and verified by 0x{FullSpeedAttributeId:X8} readback.";
            return true;
        }
        catch (Exception ex)
        {
            detail = $"Lenovo full-speed transition failed safely: {DescribeManagementFailure(ex)}";
            return false;
        }
    }

    private static (bool Present, uint Capability) ReadCapability()
    {
        using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT IDs,Capability FROM {CapabilityClass}");
        using ManagementObjectCollection collection = searcher.Get();
        foreach (ManagementObject item in collection)
        {
            using (item)
            {
                if (!TryUInt32(item["IDs"], out uint id) || id != FullSpeedAttributeId)
                    continue;
                return TryUInt32(item["Capability"], out uint capability)
                    ? (true, capability)
                    : (true, 0u);
            }
        }
        return (false, 0u);
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

    private static string DescribeManagementFailure(Exception ex) => ex is ManagementException management
        ? $"{ex.GetType().Name}/{management.ErrorCode}"
        : ex.GetType().Name;
}
