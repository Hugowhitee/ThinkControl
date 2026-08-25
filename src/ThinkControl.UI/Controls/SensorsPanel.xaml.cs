using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class SensorsPanel : UserControl
{
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromMinutes(15);
    private const int MaxPoints = 900;

    private readonly ObservableCollection<TimeSeriesPoint> _controlHistory = [];
    private readonly ObservableCollection<HardwareSensorSnapshot> _visibleSensors = [];
    private App? _app;
    private bool _statusSubscribed;
    private bool _showAllSensors;
    private AppState? _sensorState;

    public SensorsPanel()
    {
        InitializeComponent();
        SensorChart.Values = _controlHistory;
        VisibleSensorItems.ItemsSource = _visibleSensors;
        DataContextChanged += SensorsPanel_DataContextChanged;
        Loaded += SensorsPanel_Loaded;
        Unloaded += SensorsPanel_Unloaded;
        IsVisibleChanged += SensorsPanel_IsVisibleChanged;
    }

    internal void PrepareForSnapshot(AppState state)
    {
        DataContext = state;
        _controlHistory.Clear();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        double anchor = state.ControlTemperatureC ?? state.CpuTemperatureC ?? 44;
        for (int i = 0; i <= 30; i++)
        {
            double value = anchor + Math.Sin(i / 4.2) * 1.35 + Math.Cos(i / 7.0) * 0.38;
            _controlHistory.Add(new TimeSeriesPoint(now - TimeSpan.FromSeconds((30 - i) * 30), value));
        }
        GraphSubtitle.Text = string.IsNullOrWhiteSpace(state.ControlTemperatureSource)
            ? "Hottest relevant CPU/GPU thermal domain"
            : state.ControlTemperatureSource;
        AttachSensorState(state);
        RefreshVisibleSensors();
    }

    private void SensorsPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (_app is null && System.Windows.Application.Current is App app)
            _app = app;
        SyncStatusSubscription();
    }

    private void SensorsPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeStatus();
        DetachSensorState();
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

        if (!IsVisible || response?.Success != true)
            return;

        TelemetrySnapshot? telemetry = response.Telemetry;
        if (telemetry?.ControlTemperatureC is not double temperature || !double.IsFinite(temperature))
            return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (_controlHistory.Count == 0 || now - _controlHistory[^1].At >= TimeSpan.FromMilliseconds(750))
            _controlHistory.Add(new TimeSeriesPoint(now, temperature));

        DateTimeOffset cutoff = now - HistoryWindow;
        while (_controlHistory.Count > 0 && (_controlHistory[0].At < cutoff || _controlHistory.Count > MaxPoints))
            _controlHistory.RemoveAt(0);

        GraphSubtitle.Text = string.IsNullOrWhiteSpace(telemetry.ControlTemperatureSource)
            ? "Hottest relevant CPU/GPU thermal domain"
            : telemetry.ControlTemperatureSource;
    }

    private void SensorsPanel_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is AppState state)
            AttachSensorState(state);
        else
            DetachSensorState();
        RefreshVisibleSensors();
    }

    private void AttachSensorState(AppState state)
    {
        if (ReferenceEquals(_sensorState, state))
            return;
        DetachSensorState();
        _sensorState = state;
        _sensorState.Sensors.CollectionChanged += Sensors_CollectionChanged;
    }

    private void DetachSensorState()
    {
        if (_sensorState is not null)
            _sensorState.Sensors.CollectionChanged -= Sensors_CollectionChanged;
        _sensorState = null;
    }

    private void Sensors_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshVisibleSensors();

    private void RefreshVisibleSensors()
    {
        HardwareSensorSnapshot[] ordered = (_sensorState?.Sensors ?? [])
            .OrderByDescending(sensor => sensor.ControlTemperature)
            .ThenBy(sensor => SensorPriority(sensor.SensorType))
            .ThenBy(sensor => sensor.HardwareName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(sensor => sensor.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        int visibleCount = _showAllSensors ? ordered.Length : Math.Min(6, ordered.Length);
        _visibleSensors.Clear();
        foreach (HardwareSensorSnapshot sensor in ordered.Take(visibleCount))
            _visibleSensors.Add(sensor);
        ToggleSensorsButton.Visibility = ordered.Length > 6 ? Visibility.Visible : Visibility.Collapsed;
        ToggleSensorsButton.Content = _showAllSensors ? "Show less" : $"Show all {ordered.Length}";
    }

    private static int SensorPriority(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "temperature" => 0,
        "fan" => 1,
        "power" => 2,
        "load" => 3,
        _ => 4
    };

    private void ToggleSensors_Click(object sender, RoutedEventArgs e)
    {
        _showAllSensors = !_showAllSensors;
        RefreshVisibleSensors();
    }
}
