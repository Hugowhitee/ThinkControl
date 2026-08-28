using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard : UserControl
{
    private App? _app;
    private bool _viewSwitchPending;

    internal Button ExpandButtonForShellSmoke => CompactExpandButton;

    public CompactDashboard()
    {
        InitializeComponent();
        ConfigureQuickControlGeometry();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true && _app is not null)
                RefreshCompactVolume();
        };
    }

    private void ConfigureQuickControlGeometry()
    {
        // Compact selectors use the shared 40 px control height. Give the card a
        // little vertical breathing room so the ComboBox border never sits on the
        // card clip edge at the minimum Compact size.
        foreach (ComboBox combo in new[]
        {
            CompactPerformanceCombo,
            CompactFanCombo,
            CompactRefreshCombo,
            CompactKeyboardCombo
        })
        {
            combo.Margin = new Thickness(0, 6, 0, 0);
            combo.MinHeight = 40;

            if (combo.Parent is StackPanel stack && stack.Parent is Border card)
                card.Padding = new Thickness(10, 6, 10, 6);
        }
    }

    internal void Initialize(App app)
    {
        if (!ReferenceEquals(_app, app))
        {
            if (_app is not null)
            {
                _app.State.PropertyChanged -= State_PropertyChanged;
                _app.UpdateAvailabilityChanged -= App_UpdateAvailabilityChanged;
            }

            _app = app;
            app.State.PropertyChanged += State_PropertyChanged;
            app.UpdateAvailabilityChanged += App_UpdateAvailabilityChanged;
        }

        EnsureQuickControls();
        EnsureCompactMetrics();
        EnsureHardwareAlert();
        SyncQuickControls();
        SyncHardwareAlert();
    }

    private void App_UpdateAvailabilityChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(SyncHardwareAlert);

    private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.SelectedMode)
            or nameof(AppState.CoolingProfile)
            or nameof(AppState.CanFanControl)
            or nameof(AppState.RefreshAutoEnabled)
            or nameof(AppState.CurrentRefreshHz)
            or nameof(AppState.MaxRefreshHz)
            or nameof(AppState.KeyboardMode)
            or nameof(AppState.KeyboardStatus)
            or nameof(AppState.CanKeyboardBacklight))
        {
            Dispatcher.BeginInvoke(SyncQuickControls);
        }

        if (e.PropertyName is nameof(AppState.DriverStatus)
            or nameof(AppState.CanSensorTelemetry)
            or nameof(AppState.CanFanTelemetry)
            or nameof(AppState.CanKeyboardBacklight))
        {
            Dispatcher.BeginInvoke(SyncHardwareAlert);
        }
    }

    private void Expand_Click(object sender, RoutedEventArgs e) => SwitchToAdvanced("Home");

    private void SwitchToAdvanced(string page)
    {
        if (_app is null || _viewSwitchPending)
            return;

        _viewSwitchPending = true;
        MainWindow? compact = Window.GetWindow(this) as MainWindow;
        compact?.SetTransitionPending(true);

        // Let the outline paint before constructing/navigating the larger WPF tree.
        // Without this render handoff the click and the eventual window could be
        // separated by a perceptible dead period on a cold Advanced open.
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    _app.SwitchCompactToAdvanced(page);
                }
                catch (Exception ex)
                {
                    _app.RecordShellException("compact-expand", ex);
                    try { _app.CompactWindow.ShowNearTray(animate: false); } catch { }
                }
                finally
                {
                    if (compact?.IsVisible == true)
                        compact.SetTransitionPending(false);
                    _viewSwitchPending = false;
                }
            }));
        }));
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => _app?.CompactWindow.HideAnimated();

    private void OpenPage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string page })
            SwitchToAdvanced(page);
    }

    private void Battery_Click(object sender, MouseButtonEventArgs e) => SwitchToAdvanced("Battery");
}
