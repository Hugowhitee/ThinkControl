using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Controls;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.ViewModels;

public sealed class AppState : INotifyPropertyChanged
{
    private string _deviceName = "ThinkPad";
    private double? _cpuTemperatureC;
    private double? _controlTemperatureC;
    private string _controlTemperatureSource = "Unavailable";
    private int? _fanRpm;
    private string _fanStateText = "Lenovo Auto";
    private string _coolingProfile = "Lenovo Auto";
    private int _batteryPercent;
    private bool _batteryCharging;
    private string _batteryStatus = "Unknown";
    private double? _batteryPowerWatts;
    private double? _batterySmoothedPowerWatts;
    private double? _batteryHealthPercent;
    private double? _batteryTemperatureC;
    private double? _batteryRemainingWh;
    private double? _batteryFullWh;
    private TimeSpan? _batteryEtaToFull;
    private TimeSpan? _batteryEtaRemaining;
    private int? _batteryCycleCount;
    private string _batteryChargeCurveLabel = "Charge curve · learning";
    private string _batteryCurrentSessionText = "No active charge session";
    private string _batteryTypicalChargeText = "Typical charge · learning";
    private string _batteryHealthTrendText = "Health trend · learning";
    private string _batterySource = "Windows battery";
    private int _brightness = 50;
    private bool _brightnessAvailable;
    private bool? _adaptiveBrightnessEnabled;
    private bool _adaptiveBrightnessAvailable;
    private int _currentRefreshHz;
    private int _maxRefreshHz;
    private bool _refreshAutoEnabled;
    private string _hardwareAccess = "Checking…";
    private string _cpuName = "—";
    private string _gpuName = "—";
    private string _ramText = "—";
    private string _biosVersion = "—";
    private string _machineType = "—";
    private string _thermalSolution = "—";
    private string _driverStatus = "Checking hardware service…";
    private string _keyboardStatus = "Unavailable";
    private string _keyboardMode = "Auto";
    private string _keyboardBaseLevel = "High";
    private double _keyboardEffectSpeed = 1.0;
    private string _selectedMode = "Balanced";
    private string _updateStatus = "Checking automatically…";
    private bool _canFanControl;
    private bool _canFanTelemetry;
    private bool _canKeyboardBacklight;
    private bool _canCpuTemperature;
    private bool _canSensorTelemetry;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<double> TemperatureHistory { get; } = new();
    public ObservableCollection<TimeSeriesPoint> BatteryChargePowerTimeline { get; } = new ResettableObservableCollection<TimeSeriesPoint>();
    public ObservableCollection<TimeSeriesPoint> BatteryChargePercentTimeline { get; } = new ResettableObservableCollection<TimeSeriesPoint>();
    public ObservableCollection<TimeSeriesPoint> BatteryHealthTrendTimeline { get; } = new ResettableObservableCollection<TimeSeriesPoint>();
    public ObservableCollection<string> RecentChargeSessions { get; } = new ResettableObservableCollection<string>();
    public ObservableCollection<FanTelemetrySnapshot> Fans { get; } = new ResettableObservableCollection<FanTelemetrySnapshot>();
    public ObservableCollection<HardwareSensorSnapshot> Sensors { get; } = new ResettableObservableCollection<HardwareSensorSnapshot>();

    public string DeviceName { get => _deviceName; set => Set(ref _deviceName, value); }
    public double? CpuTemperatureC { get => _cpuTemperatureC; set => Set(ref _cpuTemperatureC, value); }
    public double? ControlTemperatureC { get => _controlTemperatureC; set => Set(ref _controlTemperatureC, value); }
    public string ControlTemperatureSource { get => _controlTemperatureSource; set => Set(ref _controlTemperatureSource, value); }
    public int? FanRpm { get => _fanRpm; set => Set(ref _fanRpm, value); }
    public string FanStateText { get => _fanStateText; set => Set(ref _fanStateText, value); }
    public string CoolingProfile { get => _coolingProfile; set => Set(ref _coolingProfile, string.IsNullOrWhiteSpace(value) ? "Lenovo Auto" : value); }
    public int BatteryPercent { get => _batteryPercent; set => Set(ref _batteryPercent, Math.Clamp(value, 0, 100)); }
    public bool BatteryCharging { get => _batteryCharging; set => Set(ref _batteryCharging, value); }
    public string BatteryStatus { get => _batteryStatus; set => Set(ref _batteryStatus, value); }
    public double? BatteryPowerWatts { get => _batteryPowerWatts; set => Set(ref _batteryPowerWatts, value); }
    public double? BatterySmoothedPowerWatts { get => _batterySmoothedPowerWatts; set => Set(ref _batterySmoothedPowerWatts, value); }
    public double? BatteryHealthPercent { get => _batteryHealthPercent; set => Set(ref _batteryHealthPercent, value); }
    public double? BatteryTemperatureC { get => _batteryTemperatureC; set => Set(ref _batteryTemperatureC, value); }
    public double? BatteryRemainingWh { get => _batteryRemainingWh; set => Set(ref _batteryRemainingWh, value); }
    public double? BatteryFullWh { get => _batteryFullWh; set => Set(ref _batteryFullWh, value); }
    public TimeSpan? BatteryEtaToFull { get => _batteryEtaToFull; set => Set(ref _batteryEtaToFull, value); }
    public TimeSpan? BatteryEtaRemaining { get => _batteryEtaRemaining; set => Set(ref _batteryEtaRemaining, value); }
    public int? BatteryCycleCount { get => _batteryCycleCount; set => Set(ref _batteryCycleCount, value); }
    public string BatteryChargeCurveLabel { get => _batteryChargeCurveLabel; set => Set(ref _batteryChargeCurveLabel, value); }
    public string BatteryCurrentSessionText { get => _batteryCurrentSessionText; set => Set(ref _batteryCurrentSessionText, value); }
    public string BatteryTypicalChargeText { get => _batteryTypicalChargeText; set => Set(ref _batteryTypicalChargeText, value); }
    public string BatteryHealthTrendText { get => _batteryHealthTrendText; set => Set(ref _batteryHealthTrendText, value); }
    public string BatterySource { get => _batterySource; set => Set(ref _batterySource, value); }
    public int Brightness { get => _brightness; set => Set(ref _brightness, value); }
    public bool BrightnessAvailable { get => _brightnessAvailable; set => Set(ref _brightnessAvailable, value); }
    public bool? AdaptiveBrightnessEnabled { get => _adaptiveBrightnessEnabled; set => Set(ref _adaptiveBrightnessEnabled, value); }
    public bool AdaptiveBrightnessAvailable { get => _adaptiveBrightnessAvailable; set => Set(ref _adaptiveBrightnessAvailable, value); }
    public int CurrentRefreshHz { get => _currentRefreshHz; set => Set(ref _currentRefreshHz, value); }
    public int MaxRefreshHz { get => _maxRefreshHz; set => Set(ref _maxRefreshHz, value); }
    public bool RefreshAutoEnabled { get => _refreshAutoEnabled; set => Set(ref _refreshAutoEnabled, value); }
    public string HardwareAccess { get => _hardwareAccess; set => Set(ref _hardwareAccess, value); }
    public string CpuName { get => _cpuName; set => Set(ref _cpuName, value); }
    public string GpuName { get => _gpuName; set => Set(ref _gpuName, value); }
    public string RamText { get => _ramText; set => Set(ref _ramText, value); }
    public string BiosVersion { get => _biosVersion; set => Set(ref _biosVersion, value); }
    public string MachineType { get => _machineType; set => Set(ref _machineType, value); }
    public string ThermalSolution { get => _thermalSolution; set => Set(ref _thermalSolution, value); }
    public string DriverStatus { get => _driverStatus; set => Set(ref _driverStatus, value); }
    public string KeyboardStatus { get => _keyboardStatus; set => Set(ref _keyboardStatus, value); }
    public string KeyboardMode { get => _keyboardMode; set => Set(ref _keyboardMode, value); }
    public string KeyboardBaseLevel { get => _keyboardBaseLevel; set => Set(ref _keyboardBaseLevel, value); }
    public double KeyboardEffectSpeed { get => _keyboardEffectSpeed; set => Set(ref _keyboardEffectSpeed, Math.Clamp(value, 0.5, 2.0)); }
    public string SelectedMode { get => _selectedMode; set => Set(ref _selectedMode, value); }
    public string UpdateStatus { get => _updateStatus; set => Set(ref _updateStatus, value); }
    public bool CanFanControl { get => _canFanControl; set => Set(ref _canFanControl, value); }
    public bool CanFanTelemetry { get => _canFanTelemetry; set => Set(ref _canFanTelemetry, value); }
    public bool CanKeyboardBacklight { get => _canKeyboardBacklight; set => Set(ref _canKeyboardBacklight, value); }
    public bool CanCpuTemperature { get => _canCpuTemperature; set => Set(ref _canCpuTemperature, value); }
    public bool CanSensorTelemetry { get => _canSensorTelemetry; set => Set(ref _canSensorTelemetry, value); }

    public string AppVersion => $"v{UpdateService.CurrentVersion}";
    public string CpuTemperatureText => CpuTemperatureC is double value ? $"{value:0}°C" : "—°C";
    public string ControlTemperatureText => ControlTemperatureC is double value ? $"{value:0.0} °C" : "— °C";
    public string FanRpmText => FanRpm is int value ? $"{value:N0} RPM" : "— RPM";
    public string FanCountText => Fans.Count switch
    {
        0 => "No fan telemetry",
        1 => "1 fan reading",
        _ => $"{Fans.Count} fan readings"
    };
    public string SensorCountText => Sensors.Count == 1 ? "1 live sensor" : $"{Sensors.Count:N0} live sensors";
    public string SelectedModeDisplay => SelectedMode.Equals(nameof(ThinkControlPowerMode.Quiet), StringComparison.OrdinalIgnoreCase)
        ? "Efficiency"
        : SelectedMode;
    public string CoolingProfileDisplay => CoolingProfile switch
    {
        "Silent" or "Quiet" => "Quiet",
        "Normal" or "Balanced" => "Balanced",
        "Cool" or "Max cooling" => "Max cooling",
        "Custom" => "Custom",
        _ => "Auto"
    };
    public string BatteryPercentText => $"{BatteryPercent}%";
    public string BatteryPowerText => BatteryPowerWatts is double watts ? $"{watts:0.0} W" : "— W";
    public string BatteryAveragePowerText => BatterySmoothedPowerWatts is double watts ? $"{watts:0.0} W avg" : "—";
    public string BatteryHealthText => BatteryHealthPercent is double health ? $"{health:0.#}% health" : "Health —";
    public string BatteryTemperatureText => BatteryTemperatureC is double temperature ? $"{temperature:0.#} °C" : "Not exposed";
    public string BatteryCycleCountText => BatteryCycleCount is int cycles ? $"{cycles:N0} cycles" : "Cycles —";
    public string BatteryCapacityText => BatteryRemainingWh is double remaining && BatteryFullWh is double full
        ? $"{remaining:0.#} / {full:0.#} Wh"
        : "Capacity —";
    public string BatteryEtaText => BatteryEtaToFull is TimeSpan toFull
        ? toFull <= TimeSpan.FromMinutes(1) ? "Almost full" : $"~{FormatDuration(toFull)} to full"
        : BatteryEtaRemaining is TimeSpan remaining
            ? $"~{FormatDuration(remaining)} remaining"
            : "Estimating…";
    public string BatteryCompactLine => BatteryPowerWatts.HasValue
        ? $"{BatteryPowerText} · {BatteryEtaText}"
        : BatteryStatus;
    public string BrightnessText => $"{Brightness}%";
    public string CurrentRefreshText => CurrentRefreshHz > 0 ? $"{CurrentRefreshHz} Hz" : "—";
    public string MaxRefreshText => MaxRefreshHz > 0 ? $"{MaxRefreshHz} Hz" : "Max";
    public string KeyboardModeText => KeyboardMode switch
    {
        "Breathing" => "Breathing · Low ↔ High",
        "Reactive" => $"Reactive · returns to {KeyboardBaseLevel}",
        "Audio" => "Audio reactive · experimental",
        "Auto" => "Auto · idle aware",
        _ => KeyboardStatus
    };

    public void AddTemperature(double value)
    {
        TemperatureHistory.Add(value);
        while (TemperatureHistory.Count > 60)
            TemperatureHistory.RemoveAt(0);
    }

    public void ApplyHardwareTelemetry(IReadOnlyList<FanTelemetrySnapshot>? fans, IReadOnlyList<HardwareSensorSnapshot>? sensors)
    {
        ReplaceCollection(Fans, fans ?? Array.Empty<FanTelemetrySnapshot>());
        ReplaceCollection(Sensors, sensors ?? Array.Empty<HardwareSensorSnapshot>());
        OnPropertyChanged(nameof(FanCountText));
        OnPropertyChanged(nameof(SensorCountText));
    }

    public void ClearHardwareTelemetry()
    {
        ReplaceCollection(Fans, Array.Empty<FanTelemetrySnapshot>());
        ReplaceCollection(Sensors, Array.Empty<HardwareSensorSnapshot>());
        OnPropertyChanged(nameof(FanCountText));
        OnPropertyChanged(nameof(SensorCountText));
    }

    public void ApplyBatteryHistory(BatteryHistoryView history)
    {
        BatteryChargeCurveLabel = history.ChargeCurveLabel;
        BatteryCurrentSessionText = history.CurrentSessionText;
        BatteryTypicalChargeText = history.TypicalChargeText;
        BatteryHealthTrendText = history.HealthTrendText;
        ReplaceCollection(BatteryChargePowerTimeline, history.ChargePowerTimeline);
        ReplaceCollection(BatteryChargePercentTimeline, history.ChargePercentTimeline);
        ReplaceCollection(BatteryHealthTrendTimeline, history.HealthTrendTimeline);
        ReplaceCollection(RecentChargeSessions, history.RecentSessions);
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> values)
    {
        if (target.Count == values.Count && target.SequenceEqual(values))
            return;

        if (target is ResettableObservableCollection<T> resettable)
        {
            resettable.ReplaceAll(values);
            return;
        }

        target.Clear();
        foreach (T value in values)
            target.Add(value);
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);

        if (propertyName == nameof(CpuTemperatureC))
            OnPropertyChanged(nameof(CpuTemperatureText));
        else if (propertyName == nameof(ControlTemperatureC))
            OnPropertyChanged(nameof(ControlTemperatureText));
        else if (propertyName == nameof(FanRpm))
            OnPropertyChanged(nameof(FanRpmText));
        else if (propertyName == nameof(SelectedMode))
            OnPropertyChanged(nameof(SelectedModeDisplay));
        else if (propertyName == nameof(CoolingProfile))
            OnPropertyChanged(nameof(CoolingProfileDisplay));
        else if (propertyName == nameof(BatteryPercent))
            OnPropertyChanged(nameof(BatteryPercentText));
        else if (propertyName is nameof(BatteryPowerWatts) or nameof(BatteryEtaToFull) or nameof(BatteryEtaRemaining) or nameof(BatteryStatus))
        {
            OnPropertyChanged(nameof(BatteryPowerText));
            OnPropertyChanged(nameof(BatteryEtaText));
            OnPropertyChanged(nameof(BatteryCompactLine));
        }
        else if (propertyName == nameof(BatterySmoothedPowerWatts))
            OnPropertyChanged(nameof(BatteryAveragePowerText));
        else if (propertyName == nameof(BatteryHealthPercent))
            OnPropertyChanged(nameof(BatteryHealthText));
        else if (propertyName == nameof(BatteryTemperatureC))
            OnPropertyChanged(nameof(BatteryTemperatureText));
        else if (propertyName == nameof(BatteryCycleCount))
            OnPropertyChanged(nameof(BatteryCycleCountText));
        else if (propertyName is nameof(BatteryRemainingWh) or nameof(BatteryFullWh))
            OnPropertyChanged(nameof(BatteryCapacityText));
        else if (propertyName == nameof(Brightness))
            OnPropertyChanged(nameof(BrightnessText));
        else if (propertyName == nameof(CurrentRefreshHz))
            OnPropertyChanged(nameof(CurrentRefreshText));
        else if (propertyName == nameof(MaxRefreshHz))
            OnPropertyChanged(nameof(MaxRefreshText));
        else if (propertyName is nameof(KeyboardMode) or nameof(KeyboardBaseLevel) or nameof(KeyboardStatus))
            OnPropertyChanged(nameof(KeyboardModeText));

        return true;
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
            value = TimeSpan.Zero;
        if (value.TotalHours >= 1)
            return $"{(int)value.TotalHours}h {value.Minutes:00}m";
        return $"{Math.Max(1, (int)Math.Round(value.TotalMinutes))} min";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class ResettableObservableCollection<T> : ObservableCollection<T>
    {
        internal void ReplaceAll(IReadOnlyList<T> values)
        {
            Items.Clear();
            for (int i = 0; i < values.Count; i++)
                Items.Add(values[i]);

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
