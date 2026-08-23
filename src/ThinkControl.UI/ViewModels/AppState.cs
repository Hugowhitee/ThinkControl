using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ThinkControl.UI.ViewModels;

public sealed class AppState : INotifyPropertyChanged
{
    private string _deviceName = "ThinkPad";
    private double? _cpuTemperatureC;
    private int? _fanRpm;
    private string _fanStateText = "Lenovo Auto";
    private int _batteryPercent;
    private string _batteryStatus = "Unknown";
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
    private string _driverStatus = "Checking…";
    private string _keyboardStatus = "Unavailable";
    private string _selectedMode = "Balanced";
    private string _updateStatus = "Not checked";
    private bool _canFanControl;
    private bool _canFanTelemetry;
    private bool _canKeyboardBacklight;
    private bool _canCpuTemperature;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<double> TemperatureHistory { get; } = new();

    public string DeviceName { get => _deviceName; set => Set(ref _deviceName, value); }
    public double? CpuTemperatureC { get => _cpuTemperatureC; set => Set(ref _cpuTemperatureC, value); }
    public int? FanRpm { get => _fanRpm; set => Set(ref _fanRpm, value); }
    public string FanStateText { get => _fanStateText; set => Set(ref _fanStateText, value); }
    public int BatteryPercent { get => _batteryPercent; set => Set(ref _batteryPercent, value); }
    public string BatteryStatus { get => _batteryStatus; set => Set(ref _batteryStatus, value); }
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
    public string SelectedMode { get => _selectedMode; set => Set(ref _selectedMode, value); }
    public string UpdateStatus { get => _updateStatus; set => Set(ref _updateStatus, value); }
    public bool CanFanControl { get => _canFanControl; set => Set(ref _canFanControl, value); }
    public bool CanFanTelemetry { get => _canFanTelemetry; set => Set(ref _canFanTelemetry, value); }
    public bool CanKeyboardBacklight { get => _canKeyboardBacklight; set => Set(ref _canKeyboardBacklight, value); }
    public bool CanCpuTemperature { get => _canCpuTemperature; set => Set(ref _canCpuTemperature, value); }

    public string CpuTemperatureText => CpuTemperatureC is double value ? $"{value:0}°C" : "—°C";
    public string FanRpmText => FanRpm is int value ? $"{value:N0} RPM" : "— RPM";
    public string BatteryPercentText => $"{BatteryPercent}%";
    public string BrightnessText => $"{Brightness}%";
    public string CurrentRefreshText => CurrentRefreshHz > 0 ? $"{CurrentRefreshHz} Hz" : "—";
    public string MaxRefreshText => MaxRefreshHz > 0 ? $"{MaxRefreshHz} Hz" : "Max";

    public void AddTemperature(double value)
    {
        TemperatureHistory.Add(value);
        while (TemperatureHistory.Count > 60)
            TemperatureHistory.RemoveAt(0);
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);

        if (propertyName == nameof(CpuTemperatureC))
            OnPropertyChanged(nameof(CpuTemperatureText));
        else if (propertyName == nameof(FanRpm))
            OnPropertyChanged(nameof(FanRpmText));
        else if (propertyName == nameof(BatteryPercent))
            OnPropertyChanged(nameof(BatteryPercentText));
        else if (propertyName == nameof(Brightness))
            OnPropertyChanged(nameof(BrightnessText));
        else if (propertyName == nameof(CurrentRefreshHz))
            OnPropertyChanged(nameof(CurrentRefreshText));
        else if (propertyName == nameof(MaxRefreshHz))
            OnPropertyChanged(nameof(MaxRefreshText));

        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
