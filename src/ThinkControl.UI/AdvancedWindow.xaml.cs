using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using ThinkControl.UI.Controls;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfGrid = System.Windows.Controls.Grid;
using WpfSlider = System.Windows.Controls.Slider;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace ThinkControl.UI;

public partial class AdvancedWindow : Window
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    private readonly App _app;
    private bool _forceClose;
    private bool _syncing;
    private bool _positioned;
    private UpdateCheckResult? _lastUpdate;

    public AdvancedWindow(App app)
    {
        _app = app;
        InitializeComponent();
        ConfigureNativeWindow();
        Loaded += OnLoaded;
        Closing += OnClosing;
        SourceInitialized += (_, _) => ApplyThemeToChrome();
    }

    private void ConfigureNativeWindow()
    {
        WindowChrome.SetWindowChrome(this, null);
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        ShowInTaskbar = true;

        if (Content is System.Windows.Controls.Border rootBorder)
        {
            rootBorder.CornerRadius = new CornerRadius(0);
            rootBorder.BorderThickness = new Thickness(0);
            if (rootBorder.Child is WpfGrid rootGrid && rootGrid.RowDefinitions.Count >= 2)
                rootGrid.RowDefinitions[0].Height = new GridLength(0);
        }

        AddDockControl();
    }

    private void AddDockControl()
    {
        if (NavHome.Parent is not WpfStackPanel navStack)
            return;

        var dockRow = new WpfGrid
        {
            Height = 40,
            Margin = new Thickness(10, 2, 8, 2)
        };
        dockRow.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
        dockRow.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
        dockRow.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

        var label = new WpfTextBlock
        {
            Text = "Advanced",
            FontSize = TypographyScale.Caption,
            Foreground = (System.Windows.Media.Brush)FindResource("Tc.TextFaint"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };
        dockRow.Children.Add(label);

        var notificationSlot = new WpfButton
        {
            Width = 30,
            Height = 30,
            Tag = "ThinkControl.NotificationSlot",
            Style = (Style)FindResource("TcIconButton")
        };
        WpfGrid.SetColumn(notificationSlot, 1);
        dockRow.Children.Add(notificationSlot);

        var viewbox = new PackIconLucide
        {
            Kind = "ViewSidebar",
            Width = 16,
            Height = 16,
            Foreground = (System.Windows.Media.Brush)FindResource("Tc.TextMuted")
        };
        var button = new WpfButton
        {
            Width = 32,
            Height = 32,
            ToolTip = "Switch to compact layout",
            Content = viewbox,
            Style = (Style)FindResource("TcIconButton")
        };
        button.Click += Dock_Click;
        WpfGrid.SetColumn(button, 2);
        dockRow.Children.Add(button);
        navStack.Children.Insert(0, dockRow);
    }

    private void InitializeFeaturePanels()
    {
        PerformancePanelControl.Initialize(_app);
        FansPanelControl.Initialize(_app);
        AudioPanelControl.Initialize(_app);
        TouchpadPanelControl.Initialize(_app);
    }

    public void ApplyThemeToChrome()
    {
        if (!IsSourceInitialized)
            return;

        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int useDark = ThemeService.IsLightEffective ? 0 : 1;
            _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
        }
        catch
        {
        }
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

        BeginAnimation(OpacityProperty, null);
        Opacity = 1;

        if (!IsVisible)
            Show();

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        ApplyThemeToChrome();
        UpdateLayout();
        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, new Action(static () => { }));
        Activate();
    }

    public void HideAnimated()
    {
        if (!IsVisible)
            return;

        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        Hide();
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
            case "Battery": NavBattery.IsChecked = true; break;
            case "Display": NavDisplay.IsChecked = true; break;
            case "Audio": NavAudio.IsChecked = true; break;
            case "Keyboard": NavKeyboard.IsChecked = true; break;
            case "Touchpad": NavTouchpad.IsChecked = true; break;
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

        InitializeFeaturePanels();
        StartupSwitch.IsChecked = StartupService.IsEnabled();
        ConfigureHomeQuickControls();
        SyncControls();
        ShowPage(GetSelectedPage());
        ApplyThemeToChrome();
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
            or nameof(AppState.CanFanControl)
            or nameof(AppState.CoolingProfile))
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
            ThinkControlPowerMode batteryPreference = _app.GetPowerPreference(onBattery: true);
            HomeQuiet.IsChecked = batteryPreference == ThinkControlPowerMode.Quiet;
            HomeBalanced.IsChecked = batteryPreference == ThinkControlPowerMode.Balanced;
            HomePerformance.IsChecked = batteryPreference == ThinkControlPowerMode.Performance;

            HomeRefreshAuto.IsChecked = DisplayRefreshAuto.IsChecked = state.RefreshAutoEnabled;
            bool supports60 = _app.DisplayService.GetSupportedRefreshRates().Contains(60);
            HomeRefresh60.IsEnabled = DisplayRefresh60.IsEnabled = supports60;
            HomeRefresh60.IsChecked = DisplayRefresh60.IsChecked = !state.RefreshAutoEnabled && state.CurrentRefreshHz == 60;
            bool isMax = !state.RefreshAutoEnabled && state.MaxRefreshHz > 0 && state.CurrentRefreshHz == state.MaxRefreshHz;
            HomeRefreshMax.IsChecked = DisplayRefreshMax.IsChecked = isMax;
            string maxLabel = state.MaxRefreshHz > 0 ? $"{state.MaxRefreshHz} Hz" : "Max";
            HomeRefreshMax.Content = DisplayRefreshMax.Content = maxLabel;

            HomeAdaptiveSwitch.IsChecked = DisplayAdaptiveSwitch.IsChecked = state.AdaptiveBrightnessEnabled == true;

            HomeKeyboardOff.IsEnabled = HomeKeyboardLow.IsEnabled = HomeKeyboardHigh.IsEnabled = HomeKeyboardAuto.IsEnabled =
                AdvancedKeyboardOff.IsEnabled = AdvancedKeyboardLow.IsEnabled = AdvancedKeyboardHigh.IsEnabled = AdvancedKeyboardAuto.IsEnabled = state.CanKeyboardBacklight;
            bool isStatic = state.KeyboardMode == "Static";
            HomeKeyboardOff.IsChecked = AdvancedKeyboardOff.IsChecked = isStatic && state.KeyboardStatus.Contains("Off", StringComparison.OrdinalIgnoreCase);
            HomeKeyboardLow.IsChecked = AdvancedKeyboardLow.IsChecked = isStatic && state.KeyboardStatus.Contains("Low", StringComparison.OrdinalIgnoreCase);
            HomeKeyboardHigh.IsChecked = AdvancedKeyboardHigh.IsChecked = isStatic && state.KeyboardStatus.Contains("High", StringComparison.OrdinalIgnoreCase);
            HomeKeyboardAuto.IsChecked = AdvancedKeyboardAuto.IsChecked = state.KeyboardMode == "Auto";

            if (HomeFanProfileCombo is not null)
            {
                HomeFanProfileCombo.IsEnabled = state.CanFanControl;
                HomeFanProfileCombo.SelectedItem = state.CoolingProfileDisplay;
                if (HomeFanProfileCombo.SelectedItem is null)
                    HomeFanProfileCombo.SelectedItem = "Auto";
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
            PageHome, PagePerformance, PageFans, PageBattery, PageDisplay, PageAudio,
            PageKeyboard, PageTouchpad, PageSystem, PageUpdates, PageSettings
        })
        {
            element.Visibility = Visibility.Collapsed;
        }

        FrameworkElement selected = page switch
        {
            "Performance" => PagePerformance,
            "Fans" => PageFans,
            "Battery" => PageBattery,
            "Display" => PageDisplay,
            "Audio" => PageAudio,
            "Keyboard" => PageKeyboard,
            "Touchpad" => PageTouchpad,
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
        if (NavBattery.IsChecked == true) return "Battery";
        if (NavDisplay.IsChecked == true) return "Display";
        if (NavAudio.IsChecked == true) return "Audio";
        if (NavKeyboard.IsChecked == true) return "Keyboard";
        if (NavTouchpad.IsChecked == true) return "Touchpad";
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
        if (_syncing || sender is not FrameworkElement element || element.Tag is not string tag ||
            !Enum.TryParse(tag, out ThinkControlPowerMode mode))
        {
            return;
        }

        bool homeQuickControl = element.Name.StartsWith("Home", StringComparison.Ordinal);
        bool onBattery = homeQuickControl || _app.IsCurrentlyOnBattery();
        if (!_app.SetPowerPreference(mode, onBattery))
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
        if (sender is WpfButton button &&
            button.Content?.ToString()?.Contains("Vantage", StringComparison.OrdinalIgnoreCase) == true &&
            LenovoSoftwareLauncher.TryOpenVantage())
        {
            return;
        }

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

    private void PowerOptions_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("control.exe", "/name Microsoft.PowerOptions /page pageGlobalSettings")
            {
                UseShellExecute = true
            });
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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
