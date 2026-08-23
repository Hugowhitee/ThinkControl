namespace ThinkControl.Hardware.X9;

internal static class ThinkPadRegisters
{
    internal const byte FanControl = 0x2F;
    internal const byte FanSpeedLow = 0x84;
    internal const byte FanSpeedHigh = 0x85;
    internal const byte BiosControl = 0x80;
    internal const byte MinManualLevel = 0x01;
    internal const byte MaxManualLevel = 0x07;
}

internal static class ThinkPadFanProtocol
{
    internal static int CombineRpm(byte low, byte high) => low | (high << 8);

    internal static bool IsPlausibleRpm(int rpm) => rpm is >= 0 and <= 10000;

    internal static string DescribeControl(byte value) => value switch
    {
        ThinkPadRegisters.BiosControl => "Lenovo Auto",
        >= ThinkPadRegisters.MinManualLevel and <= ThinkPadRegisters.MaxManualLevel => $"Level {value}",
        >= 0x40 and <= 0x47 => $"Override 0x{value:X2} · read-only",
        0x00 => "Fan off state · blocked",
        _ => $"EC 0x{value:X2} · unknown"
    };
}
