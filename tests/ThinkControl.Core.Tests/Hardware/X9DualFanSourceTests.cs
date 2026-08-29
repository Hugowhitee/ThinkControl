using Xunit;

namespace ThinkControl.Core.Tests.Hardware;

public sealed class X9DualFanSourceTests
{
    [Fact]
    public void X9EcContract_UsesSelectorForBothPhysicalFans()
    {
        string root = FindRepositoryRoot();
        string registers = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.Hardware", "X9", "ThinkPadRegisters.cs"));
        string ec = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.Hardware", "X9", "ThinkPadEc.cs"));

        Assert.Contains("FanSelector = 0x31", registers, StringComparison.Ordinal);
        Assert.Contains("MainFan = 0x00", registers, StringComparison.Ordinal);
        Assert.Contains("AuxiliaryFan = 0x01", registers, StringComparison.Ordinal);
        Assert.Contains("ReadFanRpms()", ec, StringComparison.Ordinal);
        Assert.Contains("ReadSelectedFanRpmUnlocked(ThinkPadRegisters.MainFan)", ec, StringComparison.Ordinal);
        Assert.Contains("ReadSelectedFanRpmUnlocked(ThinkPadRegisters.AuxiliaryFan)", ec, StringComparison.Ordinal);
        Assert.Contains("WriteAndVerifySelectedFanUnlocked(ThinkPadRegisters.MainFan", ec, StringComparison.Ordinal);
        Assert.Contains("WriteAndVerifySelectedFanUnlocked(ThinkPadRegisters.AuxiliaryFan", ec, StringComparison.Ordinal);
        Assert.Contains("TrySelectMainFanUnlocked()", ec, StringComparison.Ordinal);
    }

    [Fact]
    public void LenovoAuto_StatusDiscovery_PreservesReleasedReadOnlyPath()
    {
        string root = FindRepositoryRoot();
        string ec = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.Hardware", "X9", "ThinkPadEc.cs"));
        string controller = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.Hardware", "Lenovo", "LenovoHardwareController.cs"));

        Assert.Contains(
            "internal byte ReadFanControl() => WithEcLock(() => ReadByteUnlocked(ThinkPadRegisters.FanControl));",
            ec,
            StringComparison.Ordinal);
        Assert.Contains("internal int ReadFanRpm() => WithEcLock(ReadFanRpmUnlocked);", ec, StringComparison.Ordinal);
        Assert.Contains("if (IsThinkControlFanState(_fanControl))", controller, StringComparison.Ordinal);
        Assert.Contains("_x9FanRpm = _ec.ReadFanRpm();", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void X9Controller_ExposesBothExactEcTachometers()
    {
        string root = FindRepositoryRoot();
        string controller = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.Hardware", "Lenovo", "LenovoHardwareController.cs"));

        Assert.Contains("_ec.ReadFanRpms()", controller, StringComparison.Ordinal);
        Assert.Contains("x9-ec-main", controller, StringComparison.Ordinal);
        Assert.Contains("x9-ec-auxiliary", controller, StringComparison.Ordinal);
        Assert.Contains("ThinkPad X9 EC dual tachometers", controller, StringComparison.Ordinal);
        Assert.Contains("if (_identity.IsVerifiedX9 && ecAvailable && _x9FanRpm.HasValue && _x9AuxFanRpm.HasValue)", controller, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? current = new(start);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.Hardware")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tests", "ThinkControl.Core.Tests")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for X9 dual-fan validation.");
    }
}
