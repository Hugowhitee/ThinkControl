using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI;

public partial class MainWindow : Window
{
    private readonly App _app;
    private bool _forceClose;
    private bool _syncing;

    public MainWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public void ShowNearTray(bool animate)
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 14;
        Top = workArea.Bottom - Height - 14;

        if (!IsVisible)
            Show();

        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;

        if (animate)
        {
            Opacity = 0;
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(125))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        }
        else
        {
            Opacity = 1;
        }
    }

    public void HideAnimated()
    {
        if (!IsVisible)
            return;

        var animation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(95));
        animation.Completed += (_, _) =>
        {
            Hide();
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
        };
        BeginAnimation(OpacityProperty, animation);
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state)
            state.PropertyChanged += State_PropertyChanged;
        SyncControls();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_forceClose)
            return;

        e.Cancel = true;
        HideAnimated();
    }

    private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.SelectedMode)
            or nameof(AppState.RefreshAutoEnabled)
            or nameof(AppState.CurrentRefreshHz)
            or nameof(AppState.MaxRefreshHz)
            or nameof(AppState.AdaptiveBrightnessEnabled)
            or nameof(AppState.AdaptiveBrightnessAvailable)
            or nameof(AppState.KeyboardStatus)
            or nameof(AppState.HardwareAccess))
        {
            Dispatcher.Invoke(SyncControls);
        }
    }

    private void SyncControls()
    {
        if (DataContext is not AppState state)
            return;

        _syncing = true;
        try
        {
            QuietMode.IsChecked = state.SelectedMode == nameof(ThinkControlPowerMode.Quiet);
            BalancedMode.IsChecked = state.SelectedMode == nameof(ThinkControlPowerMode.Balanced);
            PerformanceMode.IsChecked = state.SelectedMode == nameof(ThinkControlPowerMode.Performance);

            RefreshAuto.IsChecked = state.RefreshAutoEnabled;
            Refresh60.IsChecked = !state.RefreshAutoEnabled && state.CurrentRefreshHz == 60;
            Refresh60.IsEnabled = _app.DisplayService.GetSupportedRefreshRates().Contains(60);
            RefreshMax.IsChecked = !state.RefreshAutoEnabled && state.MaxRefreshHz > 0 && state.CurrentRefreshHz == state.MaxRefreshHz;
            RefreshMax.Content = state.MaxRefreshHz > 0 ? $"{state.MaxRefreshHz} Hz" : "Max";

            AdaptiveSwitch.IsChecked = state.AdaptiveBrightnessEnabled == true;

            bool keyboardAvailable = !state.KeyboardStatus.Contains("unavailable", StringComparison.OrdinalIgnoreCase) &&
                                     !state.KeyboardStatus.Contains("Unavailable", StringComparison.OrdinalIgnoreCase);
            KeyboardOff.IsEnabled = keyboardAvailable;
            KeyboardLow.IsEnabled = keyboardAvailable;
            KeyboardHigh.IsEnabled = keyboardAvailable;
            KeyboardAuto.IsEnabled = keyboardAvailable;

            string keyboard = state.KeyboardStatus;
            KeyboardOff.IsChecked = keyboard.Contains("Off", StringComparison.OrdinalIgnoreCase);
            KeyboardLow.IsChecked = keyboard.Contains("Low", StringComparison.OrdinalIgnoreCase);
            KeyboardHigh.IsChecked = keyboard.Contains("High", StringComparison.OrdinalIgnoreCase);
            KeyboardAuto.IsChecked = keyboard.Contains("Auto", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => HideAnimated();

    private void Expand_Click(object sender, RoutedEventArgs e) => _app.OpenAdvanced("Home");

    private void OpenSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string page)
            _app.OpenAdvanced(page);
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => _app.OpenAdvanced("Settings");

    private void OpenSystem_Click(object sender, RoutedEventArgs e) => _app.OpenAdvanced("System");

    private void BatteryCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => _app.OpenAdvanced("Battery");

    private void FanCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => _app.OpenAdvanced("Fans");

    private void Mode_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not FrameworkElement { Tag: string tag } || !Enum.TryParse(tag, out ThinkControlPowerMode mode))
            return;

        if (!_app.SetPowerMode(mode))
            SyncControls();
    }

    private void RefreshAuto_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing)
            return;
        _app.EnableRefreshAuto();
        SyncControls();
    }

    private void Refresh60_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing)
            return;
        if (!_app.SetRefresh(60))
            SyncControls();
    }

    private void RefreshMax_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _app.State.MaxRefreshHz <= 0)
            return;
        if (!_app.SetRefresh(_app.State.MaxRefreshHz))
            SyncControls();
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || !IsLoaded || sender is not Slider slider || !slider.IsMouseCaptureWithin)
            return;
        _app.SetBrightness((int)Math.Round(e.NewValue));
    }

    private void AdaptiveSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing)
            return;

        bool requested = AdaptiveSwitch.IsChecked == true;
        if (!_app.SetAdaptiveBrightness(requested))
            SyncControls();
    }

    private async void Keyboard_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not FrameworkElement { Tag: string value })
            return;

        var result = await _app.HardwareClient.SetKeyboardBacklightAsync(value);
        if (result?.Success != true)
            SyncControls();
        await _app.RefreshStatusAsync();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (IsVisible)
            HideAnimated();
    }
}
