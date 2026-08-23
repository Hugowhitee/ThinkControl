namespace ThinkControl.Core.Hardware;

public enum FanStateKind
{
    LenovoAuto,
    ManualLevel
}

public readonly record struct FanState(FanStateKind Kind, byte? Level = null)
{
    public static FanState Auto => new(FanStateKind.LenovoAuto);

    public static FanState Manual(byte level) => new(FanStateKind.ManualLevel, level);
}

public readonly record struct TemperatureSample(
    double Celsius,
    string Source,
    bool IsApproximate);

public readonly record struct FanTelemetry(
    int Rpm,
    string Source);

public interface ITemperatureProvider
{
    ValueTask<TemperatureSample?> ReadAsync(CancellationToken cancellationToken);
}

public interface IFanTelemetryProvider
{
    ValueTask<FanTelemetry?> ReadAsync(CancellationToken cancellationToken);
}

public interface IFanController
{
    ValueTask<FanState> ReadStateAsync(CancellationToken cancellationToken);
    ValueTask SetStateAsync(FanState state, CancellationToken cancellationToken);
    ValueTask ReturnToAutoAsync(CancellationToken cancellationToken);
}
