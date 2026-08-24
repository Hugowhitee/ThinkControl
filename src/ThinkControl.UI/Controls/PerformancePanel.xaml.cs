using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class PerformancePanel : UserControl
{
    private App? _app;
    private DispatcherTimer? _timer;
    private bool _syncing;

    public PerformancePanel()
    {
        InitializeComponent();
        Loaded += (_, _) => StartTimer();
        Unloaded += (_, _) => StopTimer();
    }

    internal void Initialize(App app)
    {
        _app = app;
        Sync();
    }

    private void StartTimer()
    {
        if (_timer is not null)
            return;
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, (_, _) => Sync(), Dispatcher);
        _timer.Start();
        Sync();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    private void Sync()
    {
        if (_app is null)
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
            BatteryActiveBadge.Visibility = onBattery ? Visibility.Visible : Visibility.Collapsed;
            AcActiveBadge.Visibility = onBattery ? Visibility.Collapsed : Visibility.Visible;
            CurrentSourceText.Text = onBattery ? "On battery" : "Plugged in";
            ThinkControlPowerMode current = onBattery ? battery : ac;
            CurrentPowerText.Text = $"{PowerModeService.DisplayName(current)} power preference";
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
