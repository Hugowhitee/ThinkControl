namespace ThinkControl.Core.Devices;

public sealed record DeviceIdentity(
    string Manufacturer,
    string ProductName,
    string? ProductFamily,
    string? MachineType,
    string? BiosVersion);
