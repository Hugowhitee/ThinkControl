using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfSlider = System.Windows.Controls.Slider;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace ThinkControl.UI;

public partial class AdvancedWindow : Window
{
    private readonly App _app;
    private bool _forceClose;
    private bool _syncing;
    private bool _positioned;
    private bool _enhancedPagesBuilt;
    private UpdateCheckResult? _lastUpdate;

    private WpfRadioButton? _effectAuto;
    private WpfRadioButton? _effectBreathing;
    private WpfRadioButton? _effectReactive;
    private WpfRadioButton? _effectAudio;
    private WpfRadioButton? _effectBaseLow;
    private WpfRadioButton? _effectBaseHigh;
    private WpfSlider? _effectSpeed;

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
        BuildKeyboardEffectsSection();
        BuildBatteryTelemetrySection();
    }

    private void BuildKeyboardEffectsSection()
    {
        if (PageKeyboard.Content is not StackPanel root)
            return;

        var border = new Border
        {
            Style = (Style)FindResource("TcSection"),
            Margin = new Thickness(0, 14, 0, 0)
        };
        var content = new StackPanel();
        border.Child = content;

        var heading = new Grid();
        heading.Children.Add(new WpfTextBlock
        {
            Text = "Effects",
            FontWeight = FontWeights.SemiBold
        });
        var stateText = new WpfTextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = (Brush)FindResource("Tc.TextMuted")
        };
        stateText.SetBinding(TextBlock.TextProperty, new Binding(nameof(AppState.KeyboardModeText)));
        heading.Children.Add(stateText);
        content.Children.Add(heading);

        content.Children.Add(new WpfTextBlock
        {
            Text = "ThinkControl uses the X9's verified Off / Low / High levels as building blocks. If Lenovo firmware fades level changes, Breathing inherits that smooth transition without pretending the keyboard has a 0–100% PWM API.",
            Foreground = (Brush)FindResource("Tc.TextMuted"),
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 7, 0, 12)
        });

        var effects = new Grid();
        for (int i = 0; i < 4; i++) effects.ColumnDefinitions.Add(new ColumnDefinition());
        _effectAuto = CreateEffectButton("Auto", "Auto", 0, effects, new Thickness(0, 0, 5, 0));
        _effectBreathing = CreateEffectButton("Breathing", "Breathing", 1, effects, new Thickness(2, 0, 2, 0));
        _effectReactive = CreateEffectButton("Reactive", "Reactive", 2, effects, new Thickness(2, 0, 2, 0));
        _effectAudio = CreateEffectButton("Audio", "Audio", 3, effects, new Thickness(5, 0, 0, 0));
        content.Children.Add(effects);

        var baseGrid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        baseGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        baseGrid.ColumnDefinitions.Add(new ColumnDefinition());
        baseGrid.ColumnDefinitions.Add(new ColumnDefinition());
        baseGrid.Children.Add(new WpfTextBlock
        {
            Text = "Resting level",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("Tc.TextMuted")
        });
        _effectBaseLow = CreateBaseLevelButton("Low", 1, baseGrid, new Thickness(0, 0, 4, 0));
        _effectBaseHigh = CreateBaseLevelButton("High", 2, baseGrid, new Thickness(4, 0, 0, 0));
        content.Children.Add(baseGrid);

        var speedGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        speedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        speedGrid.ColumnDefinitions.Add(new ColumnDefinition());
        speedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
        speedGrid.Children.Add(new WpfTextBlock
        {
            Text = "Effect speed",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("Tc.TextMuted")
        });
        _effectSpeed = new WpfSlider
        {
            Minimum = 0.5,
            Maximum = 2.0,
            SmallChange = 0.1,
            LargeChange = 0.25,
            Value = _app.State.KeyboardEffectSpeed,
            Style = (Style)FindResource("TcSlider"),
            Margin = new Thickness(0, 0, 12, 0)
        };
        _effectSpeed.ValueChanged += EffectSpeed_ValueChanged;
        Grid.SetColumn(_effectSpeed, 1);
        speedGrid.Children.Add(_effectSpeed);
        var speedText = new WpfTextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        speedText.SetBinding(TextBlock.TextProperty, new Binding(nameof(AppState.KeyboardEffectSpeed)) { StringFormat = "{0:0.0}×" });
        Grid.SetColumn(speedText, 2);
        speedGrid.Children.Add(speedText);
        content.Children.Add(speedGrid);

        content.Children.Add(new WpfTextBlock
        {
            Text = "Reactive responds to keyboard activity only. Audio mode reads a local loopback RMS level and stores no audio. Hardware writes are deduplicated and rate-limited.",
            Foreground = (Brush)FindResource("Tc.TextFaint"),
            FontSize = 9.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        });

        root.Children.Add(border);
    }

    private WpfRadioButton CreateEffectButton(string content, string tag, int column, Grid parent, Thickness margin)
    {
        var button = new WpfRadioButton
        {
            Content = content,
            Tag = tag,
            GroupName = "AdvancedKeyboardEffect",
            Style = (Style)FindResource("TcSegment"),
            Margin = margin
        };
        button.Click += KeyboardEffect_Click;
        Grid.SetColumn(button, column);
        parent.Children.Add(button);
        return button;
    }

    private WpfRadioButton CreateBaseLevelButton(string content, int column, Grid parent, Thickness margin)
    {
        var button = new WpfRadioButton
        {
            Content = content,
            Tag = content,
            GroupName = "AdvancedKeyboardBase",
            Style = (Style)FindResource("TcSegment"),
            Margin = margin
        };
        button.Click += KeyboardBaseLevel_Click;
        Grid.SetColumn(button, column);
        parent.Children.Add(button);
        return button;
    }

    private void BuildBatteryTelemetrySection()
    {
        if (PageBattery.Content is not StackPanel root)
            return;

        var border = new Border
        {
            Style = (Style)FindResource("TcSection"),
            Margin = new Thickness(0, 14, 0, 0)
        };
        var content = new StackPanel();
        border.Child = content;
        content.Children.Add(new WpfTextBlock { Text = "Live telemetry", FontWeight = FontWeights.SemiBold });

        var metrics = new Grid { Margin = new Thickness(0, 13, 0, 0) };
        for (int i = 0; i < 4; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
        AddBatteryMetric(metrics, 0, "POWER", nameof(AppState.BatteryPowerText));
        AddBatteryMetric(metrics, 1, "ETA", nameof(AppState.BatteryEtaText));
        AddBatteryMetric(metrics, 2, "HEALTH", nameof(AppState.BatteryHealthText));
        AddBatteryMetric(metrics, 3, "ENERGY", nameof(AppState.BatteryCapacityText));
        content.Children.Add(metrics);

        var detail = new Grid { Margin = new Thickness(0, 15, 0, 0) };
        detail.ColumnDefinitions.Add(new ColumnDefinition());
        detail.ColumnDefinitions.Add(new ColumnDefinition());
        var average = CreateBoundText(nameof(AppState.BatteryAveragePowerText), 11, "Tc.TextMuted");
        detail.Children.Add(average);
        var source = CreateBoundText(nameof(AppState.BatterySource), 10.5, "Tc.TextFaint");
        source.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(source, 1);
        detail.Children.Add(source);
        content.Children.Add(detail);

        content.Children.Add(new WpfTextBlock
        {
            Text = "ETA is calculated from remaining battery energy and a median-filtered moving average of the recent charge/discharge rate. It deliberately changes slowly instead of copying a noisy one-sample estimate.",
            Foreground = (Brush)FindResource("Tc.TextFaint"),
            FontSize = 9.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 9, 0, 0)
        });

        root.Children.Add(border);
    }

    private void AddBatteryMetric(Grid parent, int column, string label, string bindingPath)
    {
        var stack = new StackPanel { Margin = new Thickness(column == 0 ? 0 : 10, 0, 0, 0) };
        stack.Children.Add(new WpfTextBlock
        {
            Text = label,
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Tc.TextMuted")
        });
        WpfTextBlock value = CreateBoundText(bindingPath, 16, "Tc.Text");
        value.Margin = new Thickness(0, 5, 0, 0);
        stack.Children.Add(value);
        Grid.SetColumn(stack, column);
        parent.Children.Add(stack);
    }

    private WpfTextBlock CreateBoundText(string path, double size, string brushResource)
    {
        var text = new WpfTextBlock
        {
            FontSize = size,
            Foreground = (Brush)FindResource(brushResource),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        text.SetBinding(TextBlock.TextProperty, new Binding(path));
        return text;
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
            or nameof(AppState.KeyboardBaseLevel)
            or nameof(AppState.KeyboardEffectSpeed)
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

            if (_effectAuto is not null)
            {
                _effectAuto.IsEnabled = _effectBreathing!.IsEnabled = _effectReactive!.IsEnabled = _effectAudio!.IsEnabled = state.CanKeyboardBacklight;
                _effectAuto.IsChecked = state.KeyboardMode == "Auto";
                _effectBreathing.IsChecked = state.KeyboardMode == "Breathing";
                _effectReactive.IsChecked = state.KeyboardMode == "Reactive";
                _effectAudio.IsChecked = state.KeyboardMode == "Audio";
                _effectBaseLow!.IsChecked = state.KeyboardBaseLevel == "Low";
                _effectBaseHigh!.IsChecked = state.KeyboardBaseLevel == "High";
                if (_effectSpeed is not null && !_effectSpeed.IsMouseCaptureWithin)
                    _effectSpeed.Value = state.KeyboardEffectSpeed;
            }

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

    private async void KeyboardEffect_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not FrameworkElement { Tag: string mode })
            return;
        await _app.SetKeyboardModeAsync(mode);
        SyncControls();
    }

    private void KeyboardBaseLevel_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not FrameworkElement { Tag: string level })
            return;
        _app.SetKeyboardBaseLevel(level);
        SyncControls();
    }

    private void EffectSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || !IsLoaded || sender is not WpfSlider slider || !slider.IsMouseCaptureWithin)
            return;
        _app.SetKeyboardEffectSpeed(e.NewValue);
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
        ThemeService.Apply(mode);
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
