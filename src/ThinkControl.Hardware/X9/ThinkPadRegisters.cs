namespace ThinkControl.Hardware.X9;

internal static class ThinkPadRegisters
{
    internal const byte FanControl = 0x2F;
    internal const byte FanSpeedLow = 0x84;
    internal const byte FanSpeedHigh = 0x85;
    internal const byte BiosControl = 0x80;
    internal const byte MinManualLevel = 0x01;
    internal const byte MaxManualLevel = 0x07;

    // Linux thinkpad_acpi defines 0x40 as the EC full-speed mode bit and ORs it
    // with level 7 before writing so level 7 remains the safety fallback when a
    // firmware ignores the override bit. ThinkControl follows the same pattern and
    // still verifies the EC readback before reporting 100% available.
    internal const byte FullSpeedBit = 0x40;
    internal const byte FullSpeedControl = FullSpeedBit | MaxManualLevel; // 0x47
}

internal static class ThinkPadFanProtocol
{
    internal static int CombineRpm(byte low, byte high) => low | (high << 8);

    internal static bool IsPlausibleRpm(int rpm) => rpm is >= 0 and <= 10000;

    internal static bool IsFullSpeed(byte value) => (value & ThinkPadRegisters.FullSpeedBit) != 0;

    internal static string DescribeControl(byte value) => value switch
    {
        ThinkPadRegisters.BiosControl => "Lenovo Auto",
        _ when IsFullSpeed(value) => "Full speed",
        >= ThinkPadRegisters.MinManualLevel and <= ThinkPadRegisters.MaxManualLevel => $"Level {value}",
        0x00 => "Fan off state · blocked",
        _ => $"EC 0x{value:X2} · unknown"
    };
}
