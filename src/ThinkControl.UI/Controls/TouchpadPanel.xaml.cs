using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ThinkControl.Core.Touchpad;
using ThinkControl.UI.Services.Touchpad;

namespace ThinkControl.UI.Controls;

public partial class TouchpadPanel : UserControl
{
    private sealed record ActionOption(GestureActionKind Action, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly DispatcherTimer _settingsSaveTimer;
    private App? _app;
    private TouchpadFeatureHost? _host;
    private TouchpadEdge _selectedEdge = TouchpadEdge.Top;
    private TouchpadGestureConfiguration _configuration =
        TouchpadGestureConfiguration.Default with { Enabled = false };
    private IReadOnlyList<TouchContact> _testContacts = Array.Empty<TouchContact>();
    private GestureSignal? _testSignal;
    private bool _syncing;
    private bool _testMode;

    public TouchpadPanel()
    {
        InitializeComponent();
        HapticStrengthSlider.Minimum = 0;
        HapticStrengthSlider.Maximum = 100;
        ClickForceSlider.Minimum = 0;
        ClickForceSlider.Maximum = 100;

        _settingsSaveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _settingsSaveTimer.Tick += (_, _) =>
        {
            _settingsSaveTimer.Stop();
            CommitGestureSettings();
        };

        ActionCombo.ItemsSource = new[]
        {
            new ActionOption(GestureActionKind.Disabled, "Off"),
            new ActionOption(GestureActionKind.Volume, "Volume"),
            new ActionOption(GestureActionKind.Brightness, "Brightness"),
            new ActionOption(GestureActionKind.MediaSeek, "Media seek"),
            new ActionOption(GestureActionKind.PreviousNextTrack, "Previous / next track"),
            new ActionOption(GestureActionKind.PlayPause, "Play / pause"),
            new ActionOption(GestureActionKind.KeyboardBacklight, "Keyboard backlight"),
            new ActionOption(GestureActionKind.PerformanceMode, "Performance mode")
        };

        Visualizer.EdgeSelected += OnEdgeSelected;
        SizeChanged += (_, _) => ApplyResponsiveLayout();
        Unloaded += OnUnloaded;
    }

    internal void Initialize(App app)
    {
        if (ReferenceEquals(_app, app))
        {
            _host?.EnsureInputStarted();
            SyncAll();
            return;
        }

        DetachHost();
        _app = app;
        _host = app.TouchpadFeature;
        _host.GestureChanged += Host_GestureChanged;
        _host.TouchpadDetected += Host_TouchpadDetected;
        _host.ContactFrameReceived += Host_ContactFrameReceived;
        _configuration = _host.Configuration.Sanitize();

        // Raw Input registration is passive until the user touches the pad. Keep it
        // active while this feature host lives so HID capabilities can be discovered
        // even when edge gestures themselves are disabled.
        _host.EnsureInputStarted();
        SyncAll();
    }

    private void SyncAll()
    {
        if (_host is null)
            return;

        _syncing = true;
        try
        {
            _configuration = _host.Configuration.Sanitize();
            GestureEnableSwitch.IsChecked = _configuration.Enabled;
            EdgeWidthSlider.Value = _configuration.EdgeWidthMm;
            ActivationSlider.Value = _configuration.ActivationDistanceMm;
            ToleranceSlider.Value = _configuration.ContinuationToleranceMm;
            Visualizer.Configuration = _configuration;
            Visualizer.SelectedEdge = _selectedEdge;
            Visualizer.Geometry = _host.Geometry ?? DefaultGeometry();
            SyncSelectedEdge();
            SyncHaptics();
            UpdateGestureLabels();
            InputStatusText.Text = _host.Geometry is null
                ? (_host.IsInputRunning ? "Waiting for touchpad input" : "Input inactive")
                : (_host.Geometry.PhysicalSizeEstimated ? "Precision Touchpad · size estimated" : "Precision Touchpad detected");
            GestureStatusText.Text = _configuration.Enabled
                ? "Edge gestures are active. Start at a highlighted edge and move in its natural direction."
                : "Edge gestures are off. Haptic settings remain independent of gestures.";
        }
        finally
        {
            _syncing = false;
        }
        ApplyResponsiveLayout();
    }

    private void SyncSelectedEdge()
    {
        TouchpadEdgeBinding binding = _configuration.BindingFor(_selectedEdge);
        SelectedZoneText.Text = EdgeLabel(_selectedEdge);
        SelectedEdgeDescription.Text = _selectedEdge switch
        {
            TouchpadEdge.Top => "Horizontal movement along the top edge.",
            TouchpadEdge.Bottom => "Horizontal movement along the bottom edge.",
            TouchpadEdge.Left => "Vertical movement along the left edge.",
            _ => "Vertical movement along the right edge."
        };
        ActionCombo.SelectedItem = ActionCombo.Items.Cast<ActionOption>()
            .First(option => option.Action == binding.Action);
        SensitivitySlider.Value = binding.Sensitivity;
        SensitivityValue.Text = $"{binding.Sensitivity:0.00}×";
        InvertCheck.IsChecked = binding.Inverted;
    }

    private void SyncHaptics()
    {
        if (_host is null)
            return;

        TouchpadHapticStatus status = _host.HapticStatus;
        bool feedbackAvailable = status.ApiAvailable && status.TouchpadPresent && status.FeedbackSupported;
        bool clickForceAvailable = feedbackAvailable && status.ClickForceSupported;

        // Never make the feature disappear. Unsupported/unavailable controls remain
        // visible and disabled so the user can see both the capability and its state.
        HapticSwitch.Visibility = Visibility.Visible;
        HapticStrengthHeader.Visibility = Visibility.Visible;
        HapticStrengthSlider.Visibility = Visibility.Visible;
        ClickForceHeader.Visibility = Visibility.Visible;
        ClickForceSlider.Visibility = Visibility.Visible;

        HapticSwitch.IsEnabled = feedbackAvailable;
        HapticStrengthSlider.IsEnabled = feedbackAvailable;
        ClickForceSlider.IsEnabled = clickForceAvailable;
        HapticSwitch.IsChecked = status.FeedbackEnabled;
        HapticStrengthSlider.Value = status.FeedbackIntensity;
        ClickForceSlider.Value = status.ClickForceSensitivity;
        HapticStrengthValue.Text = $"{status.FeedbackIntensity}%";
        ClickForceValue.Text = $"{status.ClickForceSensitivity}%";
        HapticStatusText.Margin = new Thickness(0, 4, 70, 0);

        HapticStatusText.Text = status.ApiAvailable
            ? !status.TouchpadPresent
                ? "No Windows Precision Touchpad is currently detected."
                : status.FeedbackSupported
                    ? status.ClickForceSupported
                        ? "Haptic feedback and click sensitivity are available."
                        : "Haptic feedback is available; click sensitivity is not exposed by this touchpad."
                    : "This Precision Touchpad does not report configurable haptic feedback."
            : status.FeedbackSupported
                ? $"Haptic hardware detected, but {status.Error ?? "Windows settings access is unavailable"}."
                : status.Error ?? "Haptic settings are unavailable.";
    }

    private void OnEdgeSelected(TouchpadEdge edge)
    {
        _selectedEdge = edge;
        _syncing = true;
        try { SyncSelectedEdge(); }
        finally { _syncing = false; }
    }

    private void GestureEnable_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _host is null)
            return;
        _configuration = _configuration with { Enabled = GestureEnableSwitch.IsChecked == true };
        _host.UpdateConfiguration(_configuration);
        if (_configuration.Enabled)
            _host.EnsureInputStarted();
        Visualizer.Configuration = _configuration;
        GestureStatusText.Text = _configuration.Enabled
            ? "Edge gestures are active."
            : "Edge gestures are off. Haptic settings remain independent of gestures.";
    }

    private void ActionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || ActionCombo.SelectedItem is not ActionOption option)
            return;
        TouchpadEdgeBinding current = _configuration.BindingFor(_selectedEdge);
        SetSelectedBinding(current with { Action = option.Action });
    }

    private void InvertCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing)
            return;
        TouchpadEdgeBinding current = _configuration.BindingFor(_selectedEdge);
        SetSelectedBinding(current with { Inverted = InvertCheck.IsChecked == true });
    }

    private void GestureSetting_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || !IsLoaded)
            return;
        UpdateGestureLabels();
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private void CommitGestureSettings()
    {
        if (_host is null || _syncing)
            return;

        TouchpadEdgeBinding selected = _configuration.BindingFor(_selectedEdge) with
        {
            Sensitivity = SensitivitySlider.Value
        };
        _configuration = _configuration with
        {
            EdgeWidthMm = EdgeWidthSlider.Value,
            ActivationDistanceMm = ActivationSlider.Value,
            ContinuationToleranceMm = ToleranceSlider.Value,
            Bindings = WithBinding(_configuration.Bindings ?? TouchpadGestureBindings.AsusStyle, _selectedEdge, selected)
        };
        _configuration = _configuration.Sanitize();
        _host.UpdateConfiguration(_configuration);
        Visualizer.Configuration = _configuration;
        SensitivityValue.Text = $"{selected.Sensitivity:0.00}×";
    }

    private void SetSelectedBinding(TouchpadEdgeBinding binding)
    {
        if (_host is null)
            return;
        _configuration = _configuration with
        {
            Bindings = WithBinding(_configuration.Bindings ?? TouchpadGestureBindings.AsusStyle, _selectedEdge, binding)
        };
        _configuration = _configuration.Sanitize();
        _host.UpdateConfiguration(_configuration);
        Visualizer.Configuration = _configuration;
        SensitivityValue.Text = $"{binding.Sensitivity:0.00}×";
    }

    private void HapticSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _host is null)
            return;
        if (!_host.SetHapticEnabled(HapticSwitch.IsChecked == true))
            SyncHaptics();
    }

    private void HapticSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || !IsLoaded)
            return;
        HapticStrengthValue.Text = $"{Math.Round(HapticStrengthSlider.Value)}%";
        ClickForceValue.Text = $"{Math.Round(ClickForceSlider.Value)}%";
    }

    private void HapticSlider_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_host is null)
            return;
        bool changed = ReferenceEquals(sender, HapticStrengthSlider)
            ? _host.SetHapticIntensity((int)Math.Round(HapticStrengthSlider.Value))
            : _host.SetClickForceSensitivity((int)Math.Round(ClickForceSlider.Value));
        if (!changed)
            SyncHaptics();
    }

    private void TestMode_Checked(object sender, RoutedEventArgs e)
    {
        _testMode = true;
        _testContacts = Array.Empty<TouchContact>();
        _testSignal = null;
        _host?.EnsureInputStarted();
        GestureStatusText.Text = "Test mode is active. Touch the pad to inspect gesture recognition.";
    }

    private void TestMode_Unchecked(object sender, RoutedEventArgs e)
    {
        _testMode = false;
        _testContacts = Array.Empty<TouchContact>();
        _testSignal = null;
        Visualizer.SetTestFrame(_testContacts, null);
        GestureStatusText.Text = _configuration.Enabled ? "Edge gestures are active." : "Edge gestures are off.";
    }

    private void Host_GestureChanged(GestureSignal signal)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (_testMode)
            {
                _testSignal = signal;
                Visualizer.SetTestFrame(_testContacts, _testSignal);
            }

            GestureStatusText.Text = signal.Phase switch
            {
                GesturePhase.Candidate => signal.Reason ?? "Gesture candidate",
                GesturePhase.Claimed => $"{EdgeLabel(signal.Edge)} · {ActionLabel(signal.Action)}",
                GesturePhase.Active => $"{ActionLabel(signal.Action)} · {signal.TotalTravelMm:+0.0;-0.0;0} mm",
                GesturePhase.Cancelled => $"Rejected · {signal.Reason}",
                GesturePhase.Released => "Gesture complete",
                _ => "Gesture complete"
            };
        });
    }

    private void Host_TouchpadDetected(TouchpadGeometry geometry)
    {
        Dispatcher.InvokeAsync(() =>
        {
            Visualizer.Geometry = geometry;
            InputStatusText.Text = geometry.PhysicalSizeEstimated
                ? "Precision Touchpad · size estimated"
                : "Precision Touchpad detected";
            SyncHaptics();
        });
    }

    private void Host_ContactFrameReceived(IReadOnlyList<TouchContact> contacts, TouchpadGeometry geometry)
    {
        if (!_testMode)
            return;

        TouchContact[] snapshot = contacts.ToArray();
        Dispatcher.InvokeAsync(() =>
        {
            if (!_testMode)
                return;
            _testContacts = snapshot;
            Visualizer.Geometry = geometry;
            Visualizer.SetTestFrame(_testContacts, _testSignal);
        });
    }

    private void UpdateGestureLabels()
    {
        EdgeWidthValue.Text = $"{EdgeWidthSlider.Value:0.0} mm";
        ActivationValue.Text = $"{ActivationSlider.Value:0.0} mm";
        ToleranceValue.Text = $"{ToleranceSlider.Value:0.0} mm";
        SensitivityValue.Text = $"{SensitivitySlider.Value:0.00}×";
    }

    private void ApplyResponsiveLayout()
    {
        if (ContentGrid.ActualWidth <= 0)
            return;

        while (ContentGrid.RowDefinitions.Count < 3)
            ContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        bool stacked = ContentGrid.ActualWidth < 900;
        if (stacked)
        {
            Grid.SetColumn(VisualizerCard, 0);
            Grid.SetColumnSpan(VisualizerCard, 2);
            Grid.SetRow(VisualizerCard, 0);
            VisualizerCard.Margin = new Thickness(0);

            Grid.SetColumn(SettingsStack, 0);
            Grid.SetColumnSpan(SettingsStack, 2);
            Grid.SetRow(SettingsStack, 1);
            SettingsStack.Margin = new Thickness(0, 14, 0, 0);

            Grid.SetRow(AdvancedCard, 2);
            Grid.SetColumn(AdvancedCard, 0);
            Grid.SetColumnSpan(AdvancedCard, 2);
        }
        else
        {
            Grid.SetColumn(VisualizerCard, 0);
            Grid.SetColumnSpan(VisualizerCard, 1);
            Grid.SetRow(VisualizerCard, 0);
            VisualizerCard.Margin = new Thickness(0, 0, 7, 0);

            Grid.SetColumn(SettingsStack, 1);
            Grid.SetColumnSpan(SettingsStack, 1);
            Grid.SetRow(SettingsStack, 0);
            SettingsStack.Margin = new Thickness(7, 0, 0, 0);

            Grid.SetRow(AdvancedCard, 1);
            Grid.SetColumn(AdvancedCard, 0);
            Grid.SetColumnSpan(AdvancedCard, 2);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _settingsSaveTimer.Stop();
        if (_testMode)
        {
            _testMode = false;
            _testContacts = Array.Empty<TouchContact>();
            _testSignal = null;
            TestModeSwitch.IsChecked = false;
        }
    }

    private void DetachHost()
    {
        if (_host is null)
            return;
        _host.GestureChanged -= Host_GestureChanged;
        _host.TouchpadDetected -= Host_TouchpadDetected;
        _host.ContactFrameReceived -= Host_ContactFrameReceived;
    }

    private static TouchpadGestureBindings WithBinding(
        TouchpadGestureBindings bindings,
        TouchpadEdge edge,
        TouchpadEdgeBinding binding) => edge switch
    {
        TouchpadEdge.Left => bindings with { Left = binding },
        TouchpadEdge.Right => bindings with { Right = binding },
        TouchpadEdge.Top => bindings with { Top = binding },
        TouchpadEdge.Bottom => bindings with { Bottom = binding },
        _ => bindings
    };

    private static TouchpadGeometry DefaultGeometry() => new(0, 13500, 0, 8000, 135, 80, true);

    private static string EdgeLabel(TouchpadEdge? edge) => edge switch
    {
        TouchpadEdge.Left => "Left edge",
        TouchpadEdge.Right => "Right edge",
        TouchpadEdge.Top => "Top edge",
        TouchpadEdge.Bottom => "Bottom edge",
        _ => "Corner"
    };

    private static string ActionLabel(GestureActionKind action) => action switch
    {
        GestureActionKind.Volume => "Volume",
        GestureActionKind.Brightness => "Brightness",
        GestureActionKind.MediaSeek => "Media seek",
        GestureActionKind.PreviousNextTrack => "Previous / next track",
        GestureActionKind.PlayPause => "Play / pause",
        GestureActionKind.KeyboardBacklight => "Keyboard backlight",
        GestureActionKind.PerformanceMode => "Performance mode",
        _ => "Off"
    };
}
