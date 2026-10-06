using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ThinkControl.UI.Controls;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfGrid = System.Windows.Controls.Grid;
using WpfSlider = System.Windows.Controls.Slider;
using WpfStackPanel = System.Windows.Controls.StackPanel;

namespace ThinkControl.UI;

public partial class AdvancedWindow : Window
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    private readonly App _app;
    private bool _forceClose;
    private bool _syncing;
    private bool _positioned;
    private bool _panelNavigationSubscribed;
    private UpdateCheckResult? _lastUpdate;

    public AdvancedWindow(App app)
    {
        _app = app;
        InitializeComponent();
        AddHandler(ContextTabs.NavigationRequestedEvent, new RoutedEventHandler((_, e) =>
        {
            if (e is PageNavigationEventArgs navigation) Navigate(navigation.Page);
        }));
        Loaded += OnLoaded;
        Closing += OnClosing;
        SourceInitialized += (_, _) => ApplyThemeToChrome();
    }

    private void InitializeFeaturePanels()
    {
        ModesPanelControl.Initialize(_app);
        AutomationPanelControl.Initialize(_app, automationSurface: true);
        if (!_panelNavigationSubscribed)
        {
            _panelNavigationSubscribed = true;
        }
        PerformancePanelControl.Initialize(_app);
        FansPanelControl.Initialize(_app);
        AudioPanelControl.Initialize(_app);
        HomeAudioControl.Initialize(_app);
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
            ApplyConsistentCaptionPalette();
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
        ShowPage(page);
        if (IsLoaded)
            BringSelectedNavigationIntoView();
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
        BringSelectedNavigationIntoView();
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
        if (e.PropertyName is nameof(AppState.SelectedPowerMode)
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
            SyncHomePowerModes();
            ThemeSystem.IsChecked = Services.ThemeService.Current == Services.ThemeMode.System;
            ThemeDark.IsChecked = Services.ThemeService.Current == Services.ThemeMode.Dark;
            ThemeLight.IsChecked = Services.ThemeService.Current == Services.ThemeMode.Light;

            HomeRefreshAuto.IsChecked = DisplayRefreshAuto.IsChecked = state.RefreshAutoEnabled;
            bool supports60 = _app.DisplayService.GetSupportedRefreshRates().Contains(60);
            HomeRefresh60.IsEnabled = DisplayRefresh60.IsEnabled = supports60;
            HomeRefresh60.IsChecked = DisplayRefresh60.IsChecked = !state.RefreshAutoEnabled && state.CurrentRefreshHz == 60;
            bool isMax = !state.RefreshAutoEnabled && state.MaxRefreshHz > 0 && state.CurrentRefreshHz == state.MaxRefreshHz;
            HomeRefreshMax.IsChecked = DisplayRefreshMax.IsChecked = isMax;
            string maxLabel = state.MaxRefreshHz > 0 ? $"{state.MaxRefreshHz} Hz" : "Max";
            HomeRefreshMax.Content = DisplayRefreshMax.Content = maxLabel;

            DisplayAdaptiveSwitch.IsChecked = state.AdaptiveBrightnessEnabled == true;

            HomeKeyboardOff.IsEnabled = HomeKeyboardLow.IsEnabled = HomeKeyboardHigh.IsEnabled = HomeKeyboardAuto.IsEnabled =
                AdvancedKeyboardOff.IsEnabled = AdvancedKeyboardLow.IsEnabled = AdvancedKeyboardHigh.IsEnabled = AdvancedKeyboardAuto.IsEnabled = state.CanKeyboardBacklight;
            bool isStatic = state.KeyboardMode == "Static";
            HomeKeyboardOff.IsChecked = AdvancedKeyboardOff.IsChecked = isStatic && state.KeyboardStatus.Contains("Off", StringComparison.OrdinalIgnoreCase);
            HomeKeyboardLow.IsChecked = AdvancedKeyboardLow.IsChecked = isStatic && state.KeyboardStatus.Contains("Low", StringComparison.OrdinalIgnoreCase);
            HomeKeyboardHigh.IsChecked = AdvancedKeyboardHigh.IsChecked = isStatic && state.KeyboardStatus.Contains("High", StringComparison.OrdinalIgnoreCase);
            HomeKeyboardAuto.IsChecked = AdvancedKeyboardAuto.IsChecked = state.KeyboardMode == "Auto";

            RefreshHomeCoolingSummary();
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (_selectingGroup || !IsLoaded || sender is not FrameworkElement { Tag: string page })
            return;
        ShowPage(page);
        BringSelectedNavigationIntoView();
    }

    private void BringSelectedNavigationIntoView()
    {
        if (NavHome.Parent is WpfStackPanel navStack)
            navStack.Children.OfType<System.Windows.Controls.RadioButton>()
                .FirstOrDefault(button => button.IsChecked == true)?.BringIntoView();
    }

    private void ShowPage(string page)
    {
        if (PageHome is null)
            return;

        FrameworkElement selected = page switch
        {
            "Modes" => PageModes,
            "Automation" => PageAutomation,
            "Performance" => PagePerformance,
            "Fans" => PageFans,
            "Battery" => PageBattery,
            "Display" => PageDisplay,
            "Audio" => PageAudio,
            "Keyboard" => PageKeyboard,
            "Touchpad" => PageTouchpad,
            "System" => PageSystem,
            "Updates" => PageUpdates,
            "Settings" => PageSystem,
            "Diagnostics" => PageDiagnostics,
            _ => PageHome
        };
        _selectedPage = page;
        SelectNavigationGroup(page);
        bool entering = selected.Visibility != Visibility.Visible;
        foreach (FrameworkElement element in new FrameworkElement[]
        {
            PageHome, PageModes, PageAutomation, PagePerformance, PageFans, PageBattery, PageDisplay, PageAudio,
            PageKeyboard, PageTouchpad, PageSystem, PageUpdates, PageDiagnostics
        })
            element.Visibility = ReferenceEquals(element, selected) ? Visibility.Visible : Visibility.Collapsed;
        ResetPageForNavigation((System.Windows.Controls.ScrollViewer)selected, animate: entering);
    }

    private string GetSelectedPage() => _selectedPage;

    private void HomeOpenPage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string page })
            Navigate(page);
    }

    private void PowerMode_Click(object sender, RoutedEventArgs e)
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
        SyncControls();
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
