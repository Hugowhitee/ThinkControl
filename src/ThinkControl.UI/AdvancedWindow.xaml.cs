using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ThinkControl.UI.Controls;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfSlider = System.Windows.Controls.Slider;

namespace ThinkControl.UI;

public partial class AdvancedWindow : Window
{
    private readonly App _app;
    private bool _forceClose;
    private bool _syncing;
    private bool _positioned;
    private bool _enhancedPagesBuilt;
    private UpdateCheckResult? _lastUpdate;

    public AdvancedWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public void ShowAdvanced(bool animate)
    {
        if (!_positioned)
        {
            Rect area = SystemParameters.WorkArea;
            Left = area.Left + Math.Max(18, (area.Width - Width) / 2);
            Top = area.Top + Math.Max(18, (area.Height - Height) / 2);
            _positioned = true;
        }

        if (!IsVisible)
            Show();

        WindowState = WindowState.Normal;
        Activate();

        if (animate)
        {
            Opacity = 0;
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
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

        var animation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(105));
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

    public void Navigate(string page)
    {
        switch (page)
        {
            case "Performance": NavPerformance.IsChecked = true; break;
            case "Fans": NavFans.IsChecked = true; break;
            case "Display": NavDisplay.IsChecked = true; break;
            case "Keyboard": NavKeyboard.IsChecked = true; break;
            case "Battery": NavBattery.IsChecked = true; break;
            case "System": NavSystem.IsChecked = true; break;
            case "Updates": NavUpdates.IsChecked = true; break;
            case "Settings": NavSettings.IsChecked = true; break;
            default: NavHome.IsChecked = true; break;
        }

        ShowPage(page);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state)
            state.PropertyChanged += State_PropertyChanged;

        StartupSwitch.IsChecked = StartupService.IsEnabled();
        BuildEnhancedPages();
        SyncControls();
        ShowPage(GetSelectedPage());
    }

    private void BuildEnhancedPages()
    {
        if (_enhancedPagesBuilt)
            return;
        _enhancedPagesBuilt = true;

        if (PageKeyboard.Content is System.Windows.Controls.StackPanel keyboardRoot)
        {
            keyboardRoot.Children.Add(new KeyboardEffectsPanel
            {
                DataContext = DataContext
            });
        }

        if (PageBattery.Content is System.Windows.Controls.StackPanel batteryRoot)
        {
            batteryRoot.Children.Add(new BatteryTelemetryPanel
            {
                DataContext = DataContext
            });
        }

        if (PageSettings.Content is System.Windows.Controls.StackPanel settingsRoot)
        {
            settingsRoot.Children.Add(new DiagnosticsPanel
            {
                DataContext = DataContext
            });
        }
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
            or nameof(AppState.KeyboardMode)
            or nameof(AppState.CanKeyboardBacklight)
            or nameof(AppState.CanFanControl))
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
            bool quiet = state.SelectedMode == nameof(ThinkControlPowerMode.Quiet);
            bool balanced = state.SelectedMode == nameof(ThinkControlPowerMode.Balanced);
            bool performance = state.SelectedMode == nameof(ThinkControlPowerMode.Performance);
            HomeQuiet.IsChecked = PerfQuiet.IsChecked = quiet;
            HomeBalanced.IsChecked = PerfBalanced.IsChecked = balanced;
            HomePerformance.IsChecked = PerfPerformance.IsChecked = performance;

            HomeRefreshAuto.IsChecked = DisplayRefreshAuto.IsChecked = state.RefreshAutoEnabled;
            bool supports60 = _app.DisplayService.GetSupportedRefreshRates().Contains(60);
            HomeRefresh60.IsEnabled = DisplayRefresh60.IsEnabled = supports60;
            HomeRefresh60.IsChecked = DisplayRefresh60.IsChecked = !state.RefreshAutoEnabled && state.CurrentRefreshHz == 60;
            bool isMax = !state.RefreshAutoEnabled && state.MaxRefreshHz > 0 && state.CurrentRefreshHz == state.MaxRefreshHz;
            HomeRefreshMax.IsChecked = DisplayRefreshMax.IsChecked = isMax;
            string maxLabel = state.MaxRefreshHz > 0 ? $"{state.MaxRefreshHz} Hz" : "Max";
            HomeRefreshMax.Content = DisplayRefreshMax.Content = maxLabel;

            HomeAdaptiveSwitch.IsChecked = DisplayAdaptiveSwitch.IsChecked = state.AdaptiveBrightnessEnabled == true;

            AdvancedKeyboardOff.IsEnabled = AdvancedKeyboardLow.IsEnabled = AdvancedKeyboardHigh.IsEnabled = AdvancedKeyboardAuto.IsEnabled = state.CanKeyboardBacklight;
            bool isStatic = state.KeyboardMode == "Static";
            AdvancedKeyboardOff.IsChecked = isStatic && state.KeyboardStatus.Contains("Off", StringComparison.OrdinalIgnoreCase);
            AdvancedKeyboardLow.IsChecked = isStatic && state.KeyboardStatus.Contains("Low", StringComparison.OrdinalIgnoreCase);
            AdvancedKeyboardHigh.IsChecked = isStatic && state.KeyboardStatus.Contains("High", StringComparison.OrdinalIgnoreCase);
            AdvancedKeyboardAuto.IsChecked = state.KeyboardMode == "Auto";

            foreach (WpfButton button in FindVisualChildren<WpfButton>(PageFans))
            {
                if ((button.Tag is string tag && int.TryParse(tag, out _)) || Equals(button.Content, "Lenovo Auto"))
                    button.IsEnabled = state.CanFanControl;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || sender is not FrameworkElement { Tag: string page })
            return;
        ShowPage(page);
    }

    private void ShowPage(string page)
    {
        if (PageHome is null)
            return;

        foreach (FrameworkElement element in new FrameworkElement[]
        {
            PageHome, PagePerformance, PageFans, PageDisplay, PageKeyboard, PageBattery, PageSystem, PageUpdates, PageSettings
        })
        {
            element.Visibility = Visibility.Collapsed;
        }

        FrameworkElement selected = page switch
        {
            "Performance" => PagePerformance,
            "Fans" => PageFans,
            "Display" => PageDisplay,
            "Keyboard" => PageKeyboard,
            "Battery" => PageBattery,
            "System" => PageSystem,
            "Updates" => PageUpdates,
            "Settings" => PageSettings,
            _ => PageHome
        };
        selected.Visibility = Visibility.Visible;
    }

    private string GetSelectedPage()
    {
        if (NavPerformance.IsChecked == true) return "Performance";
        if (NavFans.IsChecked == true) return "Fans";
        if (NavDisplay.IsChecked == true) return "Display";
        if (NavKeyboard.IsChecked == true) return "Keyboard";
        if (NavBattery.IsChecked == true) return "Battery";
        if (NavSystem.IsChecked == true) return "System";
        if (NavUpdates.IsChecked == true) return "Updates";
        if (NavSettings.IsChecked == true) return "Settings";
        return "Home";
    }

    private void HomeOpenPage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string page })
            Navigate(page);
    }

    private void Dock_Click(object sender, RoutedEventArgs e) => _app.ReturnToCompact();
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => _app.HideAdvancedToTray();

    private void Mode_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not FrameworkElement { Tag: string tag } || !Enum.TryParse(tag, out ThinkControlPowerMode mode))
            return;
        if (!_app.SetPowerMode(mode))
            SyncControls();
    }

    private void RefreshAuto_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        _app.EnableRefreshAuto();
        SyncControls();
    }

    private void Refresh60_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        if (!_app.SetRefresh(60)) SyncControls();
    }

    private void RefreshMax_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _app.State.MaxRefreshHz <= 0) return;
        if (!_app.SetRefresh(_app.State.MaxRefreshHz)) SyncControls();
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || !IsLoaded || sender is not WpfSlider slider || !slider.IsMouseCaptureWithin)
            return;
        _app.SetBrightness((int)Math.Round(e.NewValue));
    }

    private void AdaptiveSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not WpfCheckBox toggle)
            return;
        if (!_app.SetAdaptiveBrightness(toggle.IsChecked == true))
            SyncControls();
    }

    private async void Keyboard_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not FrameworkElement { Tag: string value })
            return;

        if (value == "Auto")
            await _app.SetKeyboardModeAsync("Auto");
        else
            await _app.SetKeyboardStaticLevelAsync(value);

        SyncControls();
    }

    private async void FanAuto_Click(object sender, RoutedEventArgs e)
    {
        var response = await _app.HardwareClient.ReturnFanToAutoAsync();
        if (response?.Success != true)
            _app.State.HardwareAccess = response?.Error ?? "Fan control unavailable";
        await _app.RefreshStatusAsync();
    }

    private async void FanLevel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string raw } || !int.TryParse(raw, out int level))
            return;
        var response = await _app.HardwareClient.SetFanLevelAsync(level);
        if (response?.Success != true)
            _app.State.HardwareAccess = response?.Error ?? "Fan control unavailable";
        await _app.RefreshStatusAsync();
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        _app.State.UpdateStatus = "Checking…";
        _lastUpdate = await _app.UpdateService.CheckAsync();
        _app.State.UpdateStatus = _lastUpdate.Status;
        OpenReleaseButton.IsEnabled = !string.IsNullOrWhiteSpace(_lastUpdate.Url);
    }

    private void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        if (_lastUpdate is not null)
            UpdateService.OpenRelease(_lastUpdate);
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not FrameworkElement { Tag: string raw } ||
            !Enum.TryParse(raw, out ThinkControl.UI.Services.ThemeMode mode))
            return;
        _app.ApplyTheme(mode);
    }

    private void StartupSwitch_Click(object sender, RoutedEventArgs e)
    {
        bool requested = StartupSwitch.IsChecked == true;
        if (!StartupService.SetEnabled(requested))
            StartupSwitch.IsChecked = !requested;
    }

    private void OpenUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string target } || string.IsNullOrWhiteSpace(target))
            return;
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
                yield return typed;
            foreach (T descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }
}
