using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class SensorsPanel : UserControl
{
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromMinutes(15);
    private const int MaxPointsPerSensor = 900;

    private readonly Dictionary<string, ObservableCollection<TimeSeriesPoint>> _history = new(StringComparer.OrdinalIgnoreCase);
    private App? _app;
    private string? _selectedSensorId;
    private bool _statusSubscribed;

    public SensorsPanel()
    {
        InitializeComponent();
        Loaded += SensorsPanel_Loaded;
        Unloaded += SensorsPanel_Unloaded;
        IsVisibleChanged += SensorsPanel_IsVisibleChanged;
    }

    internal void PrepareForSnapshot(AppState state)
    {
        DataContext = state;
        SelectPreferredSensor();

        HardwareSensorSnapshot? sensor = state.Sensors.FirstOrDefault(item =>
            string.Equals(item.Id, _selectedSensorId, StringComparison.OrdinalIgnoreCase));
        if (sensor is not null)
        {
            var points = new ObservableCollection<TimeSeriesPoint>();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            for (int i = 0; i <= 30; i++)
            {
                double phase = i / 4.2;
                double amplitude = sensor.SensorType switch
                {
                    "Temperature" => 1.35,
                    "Load" => Math.Max(2.0, sensor.Value * 0.12),
                    "Power" => Math.Max(0.35, sensor.Value * 0.07),
                    _ => Math.Max(0.2, Math.Abs(sensor.Value) * 0.025)
                };
                double value = sensor.Value + Math.Sin(phase) * amplitude + Math.Cos(i / 7.0) * amplitude * 0.28;
                points.Add(new TimeSeriesPoint(now - TimeSpan.FromSeconds((30 - i) * 30), value));
            }
            _history[sensor.Id] = points;
        }

        RefreshGraph();
    }

    private void SensorsPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (_app is null && System.Windows.Application.Current is App app)
            _app = app;

        SyncStatusSubscription();
        SelectPreferredSensor();
    }

    private void SensorsPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeStatus();
        _app = null;
    }

    private void SensorsPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        SyncStatusSubscription();

    private void SyncStatusSubscription()
    {
        bool shouldSubscribe = _app is not null && IsLoaded && IsVisible;
        if (shouldSubscribe == _statusSubscribed)
            return;

        if (shouldSubscribe)
        {
            _app!.HardwareClient.StatusObserved += HardwareClient_StatusObserved;
            _statusSubscribed = true;

            // Entering Sensors only asks the hardware cache/provider path for a fresh
            // snapshot. It must not trigger the old all-system refresh (WMI battery,
            // display discovery, power policy etc.) just because a telemetry page was
            // opened. The app-level StatusObserved handler updates shared state.
            _ = _app.HardwareClient.GetStatusAsync();
        }
        else
        {
            UnsubscribeStatus();
        }
    }

    private void UnsubscribeStatus()
    {
        if (!_statusSubscribed || _app is null)
            return;
        _app.HardwareClient.StatusObserved -= HardwareClient_StatusObserved;
        _statusSubscribed = false;
    }

    private void HardwareClient_StatusObserved(object? sender, ServiceResponse? response)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => HardwareClient_StatusObserved(sender, response));
            return;
        }

        if (!IsVisible)
            return;

        IReadOnlyList<HardwareSensorSnapshot> sensors = response?.Success == true && response.Telemetry?.Sensors is not null
            ? response.Telemetry.Sensors
            : Array.Empty<HardwareSensorSnapshot>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (HardwareSensorSnapshot sensor in sensors)
        {
            if (!_history.TryGetValue(sensor.Id, out ObservableCollection<TimeSeriesPoint>? points))
            {
                points = [];
                _history[sensor.Id] = points;
            }

            if (points.Count == 0 || now - points[^1].At >= TimeSpan.FromMilliseconds(750))
                points.Add(new TimeSeriesPoint(now, sensor.Value));

            DateTimeOffset cutoff = now - HistoryWindow;
            while (points.Count > 0 && (points[0].At < cutoff || points.Count > MaxPointsPerSensor))
                points.RemoveAt(0);
        }

        SelectPreferredSensor();
        RefreshGraph();
    }

    private void SensorPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SensorPicker.SelectedItem is HardwareSensorSnapshot sensor)
            _selectedSensorId = sensor.Id;
        RefreshGraph();
    }

    private void SelectPreferredSensor()
    {
        if (DataContext is not AppState state || state.Sensors.Count == 0)
            return;

        HardwareSensorSnapshot? desired = null;
        if (!string.IsNullOrWhiteSpace(_selectedSensorId))
            desired = state.Sensors.FirstOrDefault(sensor => string.Equals(sensor.Id, _selectedSensorId, StringComparison.OrdinalIgnoreCase));
        desired ??= state.Sensors.FirstOrDefault(sensor => sensor.ControlTemperature);
        desired ??= state.Sensors.FirstOrDefault();

        if (desired is null)
            return;
        _selectedSensorId = desired.Id;
        if (SensorPicker.SelectedItem is not HardwareSensorSnapshot selected ||
            !string.Equals(selected.Id, desired.Id, StringComparison.OrdinalIgnoreCase))
        {
            SensorPicker.SelectedItem = desired;
        }
    }

    private void RefreshGraph()
    {
        if (DataContext is not AppState state || string.IsNullOrWhiteSpace(_selectedSensorId))
        {
            SensorChart.Values = null;
            return;
        }

        HardwareSensorSnapshot? sensor = state.Sensors.FirstOrDefault(item =>
            string.Equals(item.Id, _selectedSensorId, StringComparison.OrdinalIgnoreCase));
        if (sensor is null)
            return;

        if (!_history.TryGetValue(sensor.Id, out ObservableCollection<TimeSeriesPoint>? points))
        {
            points = [];
            _history[sensor.Id] = points;
            points.Add(new TimeSeriesPoint(DateTimeOffset.UtcNow, sensor.Value));
        }

        SensorChart.Values = points;
        SensorChart.Unit = sensor.Unit;
        SensorChart.ValueFormat = sensor.SensorType switch
        {
            "Temperature" => "0.0",
            "Load" => "0.0",
            "Power" => "0.00",
            _ => "0.##"
        };
        SensorChart.IncludeZero = sensor.SensorType is "Load" or "Control";
        GraphSubtitle.Text = $"{sensor.HardwareName} · {sensor.Name} · {sensor.Source}";
    }
}
