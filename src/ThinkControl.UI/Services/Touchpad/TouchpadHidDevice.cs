using System.Runtime.InteropServices;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class TouchpadHidDevice : IDisposable
{
    private const int HidpFeature = 2;
    private const ushort HidUsagePageHaptics = 0x000E;
    private const ushort HidUsageHapticIntensity = 0x0023;
    private const ushort HidUsageButtonPressThreshold = 0x00B0;

    private readonly IntPtr _preparsedData;
    private readonly ushort[] _contactCollections;

    internal TouchpadGeometry Geometry { get; }
    internal bool SupportsHapticFeedback { get; }
    internal bool SupportsClickForce { get; }

    private TouchpadHidDevice(
        IntPtr preparsedData,
        ushort[] contactCollections,
        TouchpadGeometry geometry,
        bool supportsHapticFeedback,
        bool supportsClickForce)
    {
        _preparsedData = preparsedData;
        _contactCollections = contactCollections;
        Geometry = geometry;
        SupportsHapticFeedback = supportsHapticFeedback;
        SupportsClickForce = supportsClickForce;
    }

    internal static TouchpadHidDevice? Create(
        IntPtr rawDevice,
        double fallbackWidthMm,
        double fallbackHeightMm)
    {
        uint size = 0;
        if (TouchpadNativeMethods.GetRawInputDeviceInfo(
                rawDevice,
                TouchpadNativeMethods.RidiPreparsedData,
                IntPtr.Zero,
                ref size) != 0 || size == 0)
        {
            return null;
        }

        IntPtr preparsed = Marshal.AllocHGlobal(checked((int)size));
        bool keep = false;
        try
        {
            if (TouchpadNativeMethods.GetRawInputDeviceInfo(
                    rawDevice,
                    TouchpadNativeMethods.RidiPreparsedData,
                    preparsed,
                    ref size) == uint.MaxValue)
            {
                return null;
            }

            if (TouchpadNativeMethods.HidP_GetCaps(preparsed, out TouchpadNativeMethods.HidpCaps caps)
                != TouchpadNativeMethods.HidpStatusSuccess)
            {
                return null;
            }

            ushort valueCapsLength = caps.NumberInputValueCaps;
            if (valueCapsLength == 0)
                return null;

            var valueCaps = new TouchpadNativeMethods.HidpValueCaps[valueCapsLength];
            if (TouchpadNativeMethods.HidP_GetValueCaps(
                    TouchpadNativeMethods.HidpInput,
                    valueCaps,
                    ref valueCapsLength,
                    preparsed) != TouchpadNativeMethods.HidpStatusSuccess)
            {
                return null;
            }

            var contacts = new HashSet<ushort>();
            int xMin = 0;
            int xMax = 0;
            int yMin = 0;
            int yMax = 0;
            double widthMm = 0;
            double heightMm = 0;
            bool haveX = false;
            bool haveY = false;

            for (int i = 0; i < valueCapsLength; i++)
            {
                TouchpadNativeMethods.HidpValueCaps valueCap = valueCaps[i];
                ushort usage = valueCap.UsageMin;
                if (valueCap.UsagePage == TouchpadNativeMethods.HidUsagePageGeneric &&
                    usage == TouchpadNativeMethods.HidUsageGenericX)
                {
                    contacts.Add(valueCap.LinkCollection);
                    if (!haveX)
                    {
                        haveX = true;
                        xMin = valueCap.LogicalMin;
                        xMax = valueCap.LogicalMax;
                        widthMm = TryPhysicalLengthMm(valueCap);
                    }
                }
                else if (valueCap.UsagePage == TouchpadNativeMethods.HidUsagePageGeneric &&
                         usage == TouchpadNativeMethods.HidUsageGenericY && !haveY)
                {
                    haveY = true;
                    yMin = valueCap.LogicalMin;
                    yMax = valueCap.LogicalMax;
                    heightMm = TryPhysicalLengthMm(valueCap);
                }
            }

            if (!haveX || !haveY || contacts.Count == 0)
                return null;

            bool supportsHapticFeedback = false;
            bool supportsClickForce = false;
            ReadFeatureCapabilities(caps, preparsed, ref supportsHapticFeedback, ref supportsClickForce);

            bool estimated = widthMm <= 0 || heightMm <= 0;
            if (widthMm <= 0)
                widthMm = fallbackWidthMm;
            if (heightMm <= 0)
                heightMm = fallbackHeightMm;

            var geometry = new TouchpadGeometry(
                xMin,
                xMax,
                yMin,
                yMax,
                widthMm,
                heightMm,
                estimated);

            keep = true;
            return new TouchpadHidDevice(
                preparsed,
                contacts.Order().ToArray(),
                geometry,
                supportsHapticFeedback,
                supportsClickForce);
        }
        finally
        {
            if (!keep)
                Marshal.FreeHGlobal(preparsed);
        }
    }

    private static void ReadFeatureCapabilities(
        TouchpadNativeMethods.HidpCaps caps,
        IntPtr preparsed,
        ref bool supportsHapticFeedback,
        ref bool supportsClickForce)
    {
        ushort featureCapsLength = caps.NumberFeatureValueCaps;
        if (featureCapsLength == 0)
            return;

        var featureCaps = new TouchpadNativeMethods.HidpValueCaps[featureCapsLength];
        if (TouchpadNativeMethods.HidP_GetValueCaps(
                HidpFeature,
                featureCaps,
                ref featureCapsLength,
                preparsed) != TouchpadNativeMethods.HidpStatusSuccess)
        {
            return;
        }

        for (int i = 0; i < featureCapsLength; i++)
        {
            TouchpadNativeMethods.HidpValueCaps cap = featureCaps[i];
            if (cap.UsagePage == HidUsagePageHaptics && ContainsUsage(cap, HidUsageHapticIntensity))
                supportsHapticFeedback = true;
            else if (cap.UsagePage == TouchpadNativeMethods.HidUsagePageDigitizer &&
                     ContainsUsage(cap, HidUsageButtonPressThreshold))
                supportsClickForce = true;
        }
    }

    private static bool ContainsUsage(TouchpadNativeMethods.HidpValueCaps cap, ushort usage)
    {
        if (!cap.IsRange)
            return cap.UsageMin == usage;
        return usage >= cap.UsageMin && usage <= cap.UsageMax;
    }

    internal IReadOnlyList<TouchContact> ParseReport(IntPtr report, uint reportLength)
    {
        var contacts = new List<TouchContact>(_contactCollections.Length);

        foreach (ushort collection in _contactCollections)
        {
            if (!TryReadButtons(collection, report, reportLength, out bool tipDown, out bool confidence) || !tipDown)
                continue;

            if (!TryReadValue(TouchpadNativeMethods.HidUsagePageGeneric, collection,
                    TouchpadNativeMethods.HidUsageGenericX, report, reportLength, out uint x) ||
                !TryReadValue(TouchpadNativeMethods.HidUsagePageGeneric, collection,
                    TouchpadNativeMethods.HidUsageGenericY, report, reportLength, out uint y))
            {
                continue;
            }

            int contactId = 0;
            if (TryReadValue(TouchpadNativeMethods.HidUsagePageDigitizer, collection,
                    TouchpadNativeMethods.HidUsageContactId, report, reportLength, out uint id))
            {
                contactId = unchecked((int)id);
            }

            double? width = TryReadOptionalValue(
                TouchpadNativeMethods.HidUsageWidth, collection, report, reportLength);
            double? height = TryReadOptionalValue(
                TouchpadNativeMethods.HidUsageHeight, collection, report, reportLength);
            double? pressure = TryReadOptionalValue(
                TouchpadNativeMethods.HidUsagePressure, collection, report, reportLength);

            contacts.Add(new TouchContact(
                contactId,
                unchecked((int)x),
                unchecked((int)y),
                true,
                confidence,
                width,
                height,
                pressure));
        }

        return contacts;
    }

    private bool TryReadButtons(
        ushort collection,
        IntPtr report,
        uint reportLength,
        out bool tipDown,
        out bool confidence)
    {
        tipDown = false;
        confidence = false;

        int maxUsages = TouchpadNativeMethods.HidP_MaxUsageListLength(
            TouchpadNativeMethods.HidpInput,
            TouchpadNativeMethods.HidUsagePageDigitizer,
            _preparsedData);
        if (maxUsages <= 0)
            return false;

        var usages = new ushort[maxUsages];
        uint usageLength = checked((uint)maxUsages);
        int status = TouchpadNativeMethods.HidP_GetUsages(
            TouchpadNativeMethods.HidpInput,
            TouchpadNativeMethods.HidUsagePageDigitizer,
            collection,
            usages,
            ref usageLength,
            _preparsedData,
            report,
            reportLength);
        if (status != TouchpadNativeMethods.HidpStatusSuccess)
            return false;

        for (int i = 0; i < usageLength; i++)
        {
            if (usages[i] == TouchpadNativeMethods.HidUsageTipSwitch)
                tipDown = true;
            else if (usages[i] == TouchpadNativeMethods.HidUsageConfidence)
                confidence = true;
        }

        return true;
    }

    private bool TryReadValue(
        ushort usagePage,
        ushort collection,
        ushort usage,
        IntPtr report,
        uint reportLength,
        out uint value) =>
        TouchpadNativeMethods.HidP_GetUsageValue(
            TouchpadNativeMethods.HidpInput,
            usagePage,
            collection,
            usage,
            out value,
            _preparsedData,
            report,
            reportLength) == TouchpadNativeMethods.HidpStatusSuccess;

    private double? TryReadOptionalValue(
        ushort usage,
        ushort collection,
        IntPtr report,
        uint reportLength)
    {
        return TryReadValue(
            TouchpadNativeMethods.HidUsagePageDigitizer,
            collection,
            usage,
            report,
            reportLength,
            out uint value)
            ? value
            : null;
    }

    private static double TryPhysicalLengthMm(TouchpadNativeMethods.HidpValueCaps valueCap)
    {
        if (valueCap.PhysicalMax <= valueCap.PhysicalMin || valueCap.Units == 0)
            return 0;

        int system = unchecked((int)(valueCap.Units & 0xF));
        int lengthNibble = unchecked((int)((valueCap.Units >> 4) & 0xF));
        int lengthExponent = lengthNibble < 8 ? lengthNibble : lengthNibble - 16;
        if (lengthExponent == 0)
            return 0;

        double millimetresPerBaseUnit = system switch
        {
            1 => 10.0,
            3 => 25.4,
            _ => 0.0
        };
        if (millimetresPerBaseUnit == 0)
            return 0;

        int rawExponent = unchecked((int)(valueCap.UnitsExp & 0xF));
        int unitExponent = rawExponent < 8 ? rawExponent : rawExponent - 16;
        double span = (valueCap.PhysicalMax - valueCap.PhysicalMin) * Math.Pow(10, unitExponent);
        double millimetres = span * millimetresPerBaseUnit;
        return millimetres is >= 20 and <= 400 ? millimetres : 0;
    }

    public void Dispose()
    {
        if (_preparsedData != IntPtr.Zero)
            Marshal.FreeHGlobal(_preparsedData);
    }
}
