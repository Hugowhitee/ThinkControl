using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class PerformancePanel : UserControl
{
    private App? _app;
    private AppState? _subscribedState;
    private bool _syncing;

    public PerformancePanel()
    {
        InitializeComponent();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
                Sync();
        };
        Unloaded += (_, _) => DetachState();
    }

    internal void Initialize(App app)
    {
        if (!ReferenceEquals(_app, app))
        {
            DetachState();
            _app = app;
            _subscribedState = app.State;
            _subscribedState.PropertyChanged += State_PropertyChanged;
        }
        Sync();
    }

    private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.SelectedMode) or
            nameof(AppState.BatteryStatus) or
            nameof(AppState.BatteryCharging))
        {
            Dispatcher.BeginInvoke(Sync);
        }
    }

    private void DetachState()
    {
        if (_subscribedState is not null)
            _subscribedState.PropertyChanged -= State_PropertyChanged;
        _subscribedState = null;
    }

    internal void PrepareForSnapshot() => Sync(force: true);

    private void Sync(bool force = false)
    {
        if (_app is null || (!force && !IsVisible))
            return;

        _syncing = true;
        try
        {
            ThinkControlPowerMode battery = _app.GetPowerPreference(onBattery: true);
            ThinkControlPowerMode ac = _app.GetPowerPreference(onBattery: false);
            BatteryEfficiency.IsChecked = battery == ThinkControlPowerMode.Quiet;
            BatteryBalanced.IsChecked = battery == ThinkControlPowerMode.Balanced;
            BatteryPerformance.IsChecked = battery == ThinkControlPowerMode.Performance;
            AcEfficiency.IsChecked = ac == ThinkControlPowerMode.Quiet;
            AcBalanced.IsChecked = ac == ThinkControlPowerMode.Balanced;
            AcPerformance.IsChecked = ac == ThinkControlPowerMode.Performance;

            bool onBattery = _app.IsCurrentlyOnBattery();
            CurrentSourceText.Text = onBattery ? "On battery" : "Plugged in";
            CurrentSourceIcon.Kind = onBattery ? "BatteryHorizontal" : "BatteryChargingHorizontal";
            CurrentSourceIcon.ToolTip = onBattery ? "Running on battery" : "AC power connected";
        }
        finally
        {
            _syncing = false;
        }
    }

    private void PowerPreference_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _app is null || sender is not FrameworkElement { Tag: string tag })
            return;
        string[] parts = tag.Split(':', 2);
        if (parts.Length != 2 || !Enum.TryParse(parts[1], true, out ThinkControlPowerMode mode))
            return;
        bool onBattery = parts[0].Equals("Battery", StringComparison.OrdinalIgnoreCase);
        _ = _app.SetPowerPreference(mode, onBattery);
        Sync();
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || sender is not Button button)
            return;

        button.IsEnabled = false;
        try
        {
            _ = _app.ResetPerformanceDefaults();
            await _app.RefreshStatusAsync();
            Sync();
        }
        finally
        {
            button.IsEnabled = true;
        }
    }
}
