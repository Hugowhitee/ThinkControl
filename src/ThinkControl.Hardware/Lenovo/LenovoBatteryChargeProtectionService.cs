using System.Management;
using ThinkControl.Hardware.X9;

namespace ThinkControl.Hardware.Lenovo;

public sealed record LenovoBatteryChargeProtectionStatus(
    bool Available,
    bool Writable,
    int LimitPercent,
    bool CapabilityPresent,
    uint Capability,
    string Detail);

/// <summary>
/// Exact-X9 bridge for Lenovo Other Mode's PSU charge-type semantic.
///
/// Upstream Linux Lenovo WMI support documents PSU attribute 0x03010001 as the
/// Standard/Long-Life charge type. Long-Life limits charging at 80%; Standard
/// restores normal 100% charging. ThinkControl accepts only those two semantic
/// states and never treats this as an arbitrary battery-threshold writer.
///
/// A product write requires the exact X9 21Q6/21Q7 identity, a present capability
/// row advertising VALID+GET+SET, a live 0/1 read immediately before the write,
/// and matching post-write readback. Missing/ambiguous capability remains read-only.
/// </summary>
public static class LenovoBatteryChargeProtectionService
{
    private const string WmiNamespace = @"root\WMI";
    private const string MethodClass = "LENOVO_OTHER_METHOD";
    private const string CapabilityClass = "LENOVO_CAPABILITY_DATA_00";
    private const uint ChargeTypeAttributeId = 0x03010001;
    private const uint ChargeTypeStandard = 0;
    private const uint ChargeTypeLongLife = 1;
    private const uint SupportValid = 1u << 0;
    private const uint SupportGet = 1u << 1;
    private const uint SupportSet = 1u << 2;
    private const uint RequiredWriteSupport = SupportValid | SupportGet | SupportSet;

    public static LenovoBatteryChargeProtectionStatus Read(HardwareDeviceIdentity identity)
    {
        if (!identity.IsVerifiedX9)
        {
            return new LenovoBatteryChargeProtectionStatus(
                false, false, 100, false, 0,
                "Lenovo battery-care probing is restricted to the verified X9 21Q6/21Q7 profile.");
        }

        try
        {
            (bool capabilityPresent, uint capability) = ReadCapability();
            if (capabilityPresent && (capability & (SupportValid | SupportGet)) != (SupportValid | SupportGet))
            {
                return new LenovoBatteryChargeProtectionStatus(
                    false, false, 100, true, capability,
                    $"X9 firmware exposes 0x{ChargeTypeAttributeId:X8} without a valid readable contract (cap=0x{capability:X}).");
            }

            using ManagementObject? method = FindActiveMethodObject();
            if (method is null)
            {
                return new LenovoBatteryChargeProtectionStatus(
                    false, false, 100, capabilityPresent, capability,
                    "LENOVO_OTHER_METHOD is unavailable while probing battery charge protection.");
            }

            if (!TryGetFeatureValue(method, ChargeTypeAttributeId, out uint raw) || raw > ChargeTypeLongLife)
            {
                return new LenovoBatteryChargeProtectionStatus(
                    false, false, 100, capabilityPresent, capability,
                    $"X9 charge-type feature 0x{ChargeTypeAttributeId:X8} did not return a valid Standard/Long-Life value.");
            }

            bool writable = capabilityPresent &&
                            (capability & RequiredWriteSupport) == RequiredWriteSupport;
            int limit = raw == ChargeTypeLongLife ? 80 : 100;
            string contract = capabilityPresent
                ? $"cap=0x{capability:X}"
                : "live read only; capability row omitted";
            return new LenovoBatteryChargeProtectionStatus(
                true,
                writable,
                limit,
                capabilityPresent,
                capability,
                $"Lenovo battery charge type = {(raw == ChargeTypeLongLife ? "Long Life (80%)" : "Standard (100%)")} · {contract}");
        }
        catch (Exception ex)
        {
            return new LenovoBatteryChargeProtectionStatus(
                false, false, 100, false, 0,
                $"X9 battery-care probe failed safely: {DescribeManagementFailure(ex)}");
        }
    }

    public static bool TrySetLimit(
        HardwareDeviceIdentity identity,
        int limitPercent,
        out bool changed,
        out string? detail)
    {
        changed = false;
        detail = null;
        if (limitPercent is not 80 and not 100)
        {
            detail = "This Lenovo provider supports only 80% Battery care or 100% Full charge.";
            return false;
        }

        LenovoBatteryChargeProtectionStatus status = Read(identity);
        if (!status.Available)
        {
            detail = status.Detail;
            return false;
        }
        if (!status.Writable)
        {
            detail = $"{status.Detail} · firmware does not advertise the required VALID+GET+SET write contract.";
            return false;
        }
        if (status.LimitPercent == limitPercent)
        {
            detail = $"Battery charge protection is already {limitPercent}%; no write was needed.";
            return true;
        }

        uint requested = limitPercent == 80 ? ChargeTypeLongLife : ChargeTypeStandard;
        try
        {
            using ManagementObject? method = FindActiveMethodObject();
            if (method is null)
            {
                detail = "LENOVO_OTHER_METHOD disappeared before the battery charge transition.";
                return false;
            }

            if (!TryGetFeatureValue(method, ChargeTypeAttributeId, out uint before) || before > ChargeTypeLongLife)
            {
                detail = "The X9 battery charge-type feature no longer returns a safe Standard/Long-Life value.";
                return false;
            }
            if (before == requested)
            {
                detail = $"Battery charge protection is already {limitPercent}%; no write was needed.";
                return true;
            }

            if (!TrySetFeatureValue(method, ChargeTypeAttributeId, requested, out string? setError))
            {
                detail = setError ?? "Lenovo firmware rejected the battery charge-type transition.";
                return false;
            }
            if (!TryGetFeatureValue(method, ChargeTypeAttributeId, out uint verified) || verified != requested)
            {
                detail = $"Battery charge-type readback did not verify {limitPercent}%; ThinkControl is leaving the firmware-reported state unchanged.";
                return false;
            }

            changed = true;
            detail = $"Battery charge protection set to {limitPercent}% and verified by Lenovo firmware readback.";
            return true;
        }
        catch (Exception ex)
        {
            detail = $"Battery charge protection failed safely: {DescribeManagementFailure(ex)}";
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
                if (!TryUInt32(item["IDs"], out uint id) || id != ChargeTypeAttributeId)
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
