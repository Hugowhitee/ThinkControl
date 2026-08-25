using System.Collections.ObjectModel;
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
    private App? _app;
    private bool _statusSubscribed;

    public SensorsPanel()
    {
        InitializeComponent();
        SensorChart.Values = _controlHistory;
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
}
