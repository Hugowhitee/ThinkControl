using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ThinkControl.Core.Touchpad;
using ThinkControl.UI.Services.Touchpad;

namespace ThinkControl.UI.Controls;

public partial class TouchpadPanel : UserControl
{
    private sealed record ActionOption(GestureActionKind Action, string Label, string Description)
    {
        public override string ToString() => Label;
    }

    private readonly DispatcherTimer _settingsSaveTimer;
    private App? _app;
    private TouchpadFeatureHost? _host;
    private TouchpadEdge _selectedEdge = TouchpadEdge.Top;
    private TouchpadGestureConfiguration _configuration =
        TouchpadGestureConfiguration.Default with { Enabled = false };
    private IReadOnlyList<TouchContact> _contacts = Array.Empty<TouchContact>();
    private GestureSignal? _signal;
    private bool _syncing;
    private bool _hostUiSubscribed;

    public TouchpadPanel()
    {
        InitializeComponent();
        HapticStrengthSlider.Minimum = 0;
        HapticStrengthSlider.Maximum = 100;
        ClickForceSlider.Minimum = 0;
        ClickForceSlider.Maximum = 100;
        OsdPositionCombo.ItemsSource = new[] { "Left", "Center", "Right" };

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
            new ActionOption(GestureActionKind.Disabled, "Off", "Leave this edge unassigned."),
            new ActionOption(GestureActionKind.Volume, "Volume", "Slide continuously to change volume. The speaker button in the pop-up handles mute and unmute."),
            new ActionOption(GestureActionKind.Brightness, "Brightness", "Slide continuously to change Windows display brightness."),
            new ActionOption(GestureActionKind.MediaSeek, "Media scrub", "Scrub through the active media session. Slow movement is precise; faster movement seeks farther."),
            new ActionOption(GestureActionKind.PreviousNextTrack, "Track control", "Swipe one way for previous and the other for next. Optional center hold can play or pause without triggering while you swipe through it."),
            new ActionOption(GestureActionKind.PlayPause, "Play / pause", "Toggle the active media session once when the edge gesture is claimed."),
            new ActionOption(GestureActionKind.OpenThinkControl, "Open ThinkControl", "Open the Compact ThinkControl surface from this edge gesture.")
        };

        Visualizer.EdgeSelected += OnEdgeSelected;
        SizeChanged += (_, _) => ApplyResponsiveLayout();
        IsVisibleChanged += (_, e) => OnVisibilityChanged(e.NewValue is true);
        Loaded += (_, _) => SyncHostUiSubscriptions(IsVisible);
        Unloaded += OnUnloaded;
    }

    internal void Initialize(App app)
    {
        if (ReferenceEquals(_app, app))
        {
            SyncHostUiSubscriptions(IsVisible);
            if (IsVisible || _host?.Configuration.Enabled == true)
                _host?.EnsureInputStarted();
            SyncAll();
            return;
        }

        DetachHost();
        _app = app;
        _host = app.TouchpadFeature;
        _configuration = _host.Configuration.Sanitize();
        SyncHostUiSubscriptions(IsVisible);
        if (IsVisible || _configuration.Enabled)
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
            Visualizer.SetTestFrame(_contacts, _signal);
            SyncSelectedEdge();
            SyncHaptics();
            SyncOsd();
            UpdateGestureLabels();
            InputStatusText.Text = _host.Geometry is null
                ? (_host.IsInputRunning ? "Waiting for touchpad input" : "Input inactive")
                : (_host.Geometry.PhysicalSizeEstimated ? "Precision Touchpad · size estimated" : "Precision Touchpad detected");
            GestureStatusText.Text = _configuration.Enabled
                ? "Edge gestures are active. Start inside a highlighted edge band and move along that edge."
                : "Edge gestures are off. Live touch visualization runs only while this page is open.";
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
        SelectedEdgeDescription.Text = _selectedEdge switch
        {
            TouchpadEdge.Top => "Horizontal movement along the top edge.",
            TouchpadEdge.Bottom => "Horizontal movement along the bottom edge.",
            TouchpadEdge.Left => "Vertical movement along the left edge.",
            _ => "Vertical movement along the right edge."
        };
        ActionOption selected = ActionCombo.Items.Cast<ActionOption>()
            .FirstOrDefault(option => option.Action == binding.Action)
            ?? ActionCombo.Items.Cast<ActionOption>().First(option => option.Action == GestureActionKind.Disabled);
        ActionCombo.SelectedItem = selected;
        ActionHelpText.Text = selected.Description;
        SensitivitySlider.Value = binding.Sensitivity;
        SensitivityValue.Text = FormatSensitivity(binding.Sensitivity);
        InvertCheck.IsChecked = binding.Inverted;
    }

    private void SyncHaptics()
    {
        if (_host is null)
            return;

        TouchpadHapticStatus status = _host.HapticStatus;
        bool feedbackAvailable = status.ApiAvailable && status.TouchpadPresent && status.FeedbackSupported;
        bool clickForceAvailable = feedbackAvailable && status.ClickForceSupported;

        HapticSwitch.IsEnabled = feedbackAvailable;
        HapticStrengthSlider.IsEnabled = feedbackAvailable;
        ClickForceSlider.IsEnabled = clickForceAvailable;
        HapticSwitch.IsChecked = status.FeedbackEnabled;
        HapticStrengthSlider.Value = Quantize(status.FeedbackIntensity, 25);
        ClickForceSlider.Value = Quantize(status.ClickForceSensitivity, 50);
        HapticStrengthValue.Text = FeedbackLevel((int)HapticStrengthSlider.Value);
        ClickForceValue.Text = ClickLevel((int)ClickForceSlider.Value);

        HapticStatusText.Text = status.ApiAvailable
            ? !status.TouchpadPresent
                ? "No Windows Precision Touchpad is currently detected."
                : status.FeedbackSupported
                    ? status.ClickForceSupported
                        ? "Uses the same discrete levels as Windows touchpad settings."
                        : "Haptic feedback is available; click sensitivity is not exposed by this touchpad."
                    : "This Precision Touchpad does not report configurable haptic feedback."
            : status.FeedbackSupported
                ? $"Haptic hardware detected, but {status.Error ?? "Windows settings access is unavailable"}."
                : status.Error ?? "Haptic settings are unavailable.";
    }

    private void SyncOsd()
    {
        if (_app is null)
            return;
        var settings = _app.UserSettings.Current;
        OsdSwitch.IsChecked = settings.TouchpadOsdEnabled;
        OsdPositionCombo.SelectedItem = settings.TouchpadOsdPosition;
        OsdOpacitySlider.Value = Math.Round(settings.TouchpadOsdOpacity * 100);
        OsdOpacityValue.Text = $"{Math.Round(settings.TouchpadOsdOpacity * 100)}%";
        OsdPositionCombo.IsEnabled = settings.TouchpadOsdEnabled;
        OsdOpacitySlider.IsEnabled = settings.TouchpadOsdEnabled;
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
        else if (!IsVisible)
            _host.StopInputIfGesturesDisabled();
        Visualizer.Configuration = _configuration;
        GestureStatusText.Text = _configuration.Enabled
            ? "Edge gestures are active."
            : "Edge gestures are off. Live visualization stops when you leave this page.";
    }

    private void ActionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || ActionCombo.SelectedItem is not ActionOption option)
            return;
        ActionHelpText.Text = option.Description;
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
        SensitivityValue.Text = FormatSensitivity(selected.Sensitivity);
    }

    private void SetSelectedBinding(TouchpadEdgeBinding binding)
    {
        if (_host is null)
            return;

        TouchpadGestureBindings bindings = _configuration.Bindings ?? TouchpadGestureBindings.AsusStyle;
        TouchpadEdge? movedFrom = null;
        if (binding.Action != GestureActionKind.Disabled)
        {
            foreach (TouchpadEdge edge in Enum.GetValues<TouchpadEdge>())
            {
                if (edge == _selectedEdge)
                    continue;
                TouchpadEdgeBinding existing = bindings.Get(edge).Sanitize();
                if (existing.Action != binding.Action)
                    continue;
                movedFrom = edge;
                bindings = WithBinding(bindings, edge, existing with { Action = GestureActionKind.Disabled });
            }
        }

        bindings = WithBinding(bindings, _selectedEdge, binding);
        _configuration = (_configuration with { Bindings = bindings }).Sanitize();
        _host.UpdateConfiguration(_configuration);
        Visualizer.Configuration = _configuration;
        SensitivityValue.Text = FormatSensitivity(binding.Sensitivity);

        if (movedFrom is TouchpadEdge previous)
            GestureStatusText.Text = $"{ActionLabel(binding.Action)} moved from {EdgeLabel(previous)} to {EdgeLabel(_selectedEdge)}.";
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
        HapticStrengthValue.Text = FeedbackLevel((int)Quantize(HapticStrengthSlider.Value, 25));
        ClickForceValue.Text = ClickLevel((int)Quantize(ClickForceSlider.Value, 50));
    }

    private void HapticSlider_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_host is null)
            return;
        bool strength = ReferenceEquals(sender, HapticStrengthSlider);
        int value = (int)Quantize(strength ? HapticStrengthSlider.Value : ClickForceSlider.Value, strength ? 25 : 50);
        bool changed = strength
            ? _host.SetHapticIntensity(value)
            : _host.SetClickForceSensitivity(value);
        if (!changed)
            SyncHaptics();
        else
        {
            if (strength) HapticStrengthSlider.Value = value;
            else ClickForceSlider.Value = value;
        }
    }

    private void OsdSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _app is null)
            return;
        bool enabled = OsdSwitch.IsChecked == true;
        _app.UserSettings.Update(settings => settings with { TouchpadOsdEnabled = enabled });
        OsdPositionCombo.IsEnabled = enabled;
        OsdOpacitySlider.IsEnabled = enabled;
    }

    private void OsdPosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _app is null || OsdPositionCombo.SelectedItem is not string position)
            return;
        _app.UserSettings.Update(settings => settings with { TouchpadOsdPosition = position });
    }

    private void OsdOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || !IsLoaded)
            return;
        OsdOpacityValue.Text = $"{Math.Round(e.NewValue)}%";
    }

    private void OsdOpacity_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_app is null)
            return;
        double opacity = Math.Clamp(OsdOpacitySlider.Value / 100d, 0, 1.0);
        _app.UserSettings.Update(settings => settings with { TouchpadOsdOpacity = opacity });
    }

    private void Host_GestureChanged(GestureSignal signal)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (!IsVisible || !_hostUiSubscribed)
                return;

            UpdateGestureValueFeedback(signal);
            _signal = signal.Phase is GesturePhase.Released or GesturePhase.Cancelled ? null : signal;
            Visualizer.SetTestFrame(_contacts, _signal);
            GestureStatusText.Text = signal.Phase switch
            {
                GesturePhase.Candidate when signal.Action == GestureActionKind.PreviousNextTrack && _configuration.TrackCenterPlayPauseEnabled =>
                    "Track control · hold for Play / Pause, or swipe for Previous / Next",
                GesturePhase.Candidate => signal.Reason ?? "Gesture candidate",
                GesturePhase.Claimed => $"{EdgeLabel(signal.Edge)} · {ActionLabel(signal.Action)}",
                GesturePhase.Active => FormatGestureStatus(signal),
                GesturePhase.Cancelled => $"Rejected · {signal.Reason}",
                GesturePhase.Released => $"Gesture complete · {FormatGestureStatus(signal)}",
                _ => "Gesture complete"
            };
        });
    }

    private void Host_TouchpadDetected(TouchpadGeometry geometry)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (!IsVisible || !_hostUiSubscribed)
                return;
            Visualizer.Geometry = geometry;
            InputStatusText.Text = geometry.PhysicalSizeEstimated
                ? "Precision Touchpad · size estimated"
                : "Precision Touchpad detected";
            SyncHaptics();
        });
    }

    private void Host_ContactFrameReceived(IReadOnlyList<TouchContact> contacts, TouchpadGeometry geometry)
    {
        TouchContact[] snapshot = contacts.ToArray();
        Dispatcher.InvokeAsync(() =>
        {
            if (!IsVisible || !_hostUiSubscribed)
                return;
            _contacts = snapshot;
            Visualizer.Geometry = geometry;
            Visualizer.SetTestFrame(_contacts, _signal);
        });
    }

    private void UpdateGestureLabels()
    {
        EdgeWidthValue.Text = $"{EdgeWidthSlider.Value:0.0} mm";
        ActivationValue.Text = $"{ActivationSlider.Value:0.0} mm";
        ToleranceValue.Text = $"{ToleranceSlider.Value:0.0} mm";
        SensitivityValue.Text = FormatSensitivity(SensitivitySlider.Value);
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

    private void OnVisibilityChanged(bool visible)
    {
        if (_host is null)
            return;

        SyncHostUiSubscriptions(visible);
        if (visible)
        {
            ClearGestureFeedback();
            _host.EnsureInputStarted();
            SyncAll();
            return;
        }

        _settingsSaveTimer.Stop();
        _contacts = Array.Empty<TouchContact>();
        _signal = null;
        ClearGestureFeedback();
        Visualizer.SetTestFrame(_contacts, null);
        _host.StopInputIfGesturesDisabled();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _settingsSaveTimer.Stop();
        ClearGestureFeedback();
        SyncHostUiSubscriptions(false);
        _host?.StopInputIfGesturesDisabled();
    }

    private void SyncHostUiSubscriptions(bool subscribe)
    {
        if (_host is null || subscribe == _hostUiSubscribed)
            return;

        if (subscribe)
        {
            _host.GestureChanged += Host_GestureChanged;
            _host.TouchpadDetected += Host_TouchpadDetected;
            _host.ContactFrameReceived += Host_ContactFrameReceived;
            _hostUiSubscribed = true;
            return;
        }

        _host.GestureChanged -= Host_GestureChanged;
        _host.TouchpadDetected -= Host_TouchpadDetected;
        _host.ContactFrameReceived -= Host_ContactFrameReceived;
        _hostUiSubscribed = false;
    }

    private void DetachHost()
    {
        SyncHostUiSubscriptions(false);
        _host = null;
    }

    private static string FormatSensitivity(double value) => $"{value:0.0}x";

    private static double Quantize(double value, int step) => Math.Clamp(Math.Round(value / step) * step, 0, 100);

    private static string FeedbackLevel(int value) => value switch
    {
        <= 0 => "Off",
        <= 25 => "Low",
        <= 50 => "Medium",
        <= 75 => "High",
        _ => "Strong"
    };

    private static string ClickLevel(int value) => value switch
    {
        <= 0 => "Firm",
        <= 50 => "Medium",
        _ => "Light"
    };

    private static TouchpadGestureBindings WithBinding(TouchpadGestureBindings bindings, TouchpadEdge edge, TouchpadEdgeBinding binding) => edge switch
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
        GestureActionKind.MediaSeek => "Media scrub",
        GestureActionKind.PreviousNextTrack => "Track control",
        GestureActionKind.PlayPause => "Play / pause",
        GestureActionKind.OpenThinkControl => "Open ThinkControl",
        _ => "Off"
    };
}
