using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Controls;

public partial class TouchpadPanel
{
    private readonly Dictionary<Slider, Button> _sliderResetButtons = new();
    private bool _valueFeedbackConfigured;
    private int? _gestureStartValue;

    internal void ConfigureValueFeedback()
    {
        if (_valueFeedbackConfigured)
            return;
        _valueFeedbackConfigured = true;

        EdgeWidthSlider.ValueChanged += (_, _) => RefreshValueFeedback();
        ActivationSlider.ValueChanged += (_, _) => RefreshValueFeedback();
        ToleranceSlider.ValueChanged += (_, _) => RefreshValueFeedback();
        SensitivitySlider.ValueChanged += (_, _) => RefreshValueFeedback();
        HapticStrengthSlider.ValueChanged += (_, _) => RefreshSliderResetButtons();
        ClickForceSlider.ValueChanged += (_, _) => RefreshSliderResetButtons();
        OsdOpacitySlider.ValueChanged += (_, _) => RefreshSliderResetButtons();
        Loaded += (_, _) => RefreshValueFeedback();

        ConfigureResetButton(EdgeWidthSlider, EdgeWidthValue, 5.0, ResetGestureSlider, "Touchpad");
        ConfigureResetButton(ActivationSlider, ActivationValue, 2.0, ResetGestureSlider, "Gauge");
        ConfigureResetButton(ToleranceSlider, ToleranceValue, 12.0, ResetGestureSlider, "Gauge");
        ConfigureResetButton(SensitivitySlider, SensitivityValue, 1.0, ResetGestureSlider, "Gauge");
        ConfigureResetButton(HapticStrengthSlider, HapticStrengthValue, App.DefaultHapticFeedbackIntensity, ResetHapticSlider, "Touchpad");
        ConfigureResetButton(ClickForceSlider, ClickForceValue, App.DefaultHapticClickSensitivity, ResetHapticSlider, "Touchpad");
        ConfigureResetButton(OsdOpacitySlider, OsdOpacityValue, 92.0, ResetOsdOpacity, "Monitor");

        // GestureChanged is owned by TouchpadPanel.xaml.cs. Keeping a second event
        // handler here used to dispatch hidden-page UI work and race the status text.
        // Value feedback is now called from that one page-visible gesture path.
        RefreshValueFeedback();
    }

    internal void PrepareForSnapshot(bool showActiveGesture)
    {
        _settingsSaveTimer.Stop();
        ClearGestureFeedback();
        InputStatusText.Text = "Precision Touchpad detected";

        if (!showActiveGesture)
        {
            Visualizer.SetTestFrame(Array.Empty<TouchContact>(), null);
            return;
        }

        _syncing = true;
        try
        {
            _selectedEdge = TouchpadEdge.Left;
            GestureEnableSwitch.IsChecked = true;
            Visualizer.SelectedEdge = _selectedEdge;
            Visualizer.Configuration = _configuration with { Enabled = true };
            SyncSelectedEdge();
        }
        finally
        {
            _syncing = false;
        }

        var signal = new GestureSignal(
            GesturePhase.Active,
            TouchpadEdge.Left,
            GestureActionKind.Volume,
            TotalTravelMm: 24.8,
            DeltaMm: 3.1,
            ContactId: 1);

        // Two deterministic frames exercise both the red contact point and trail.
        // The value card itself is populated directly because snapshot mode must not
        // write the real Windows volume endpoint merely to create visual QA data.
        Visualizer.SetTestFrame([new TouchContact(1, 420, 6400, true)], signal);
        Visualizer.SetTestFrame([new TouchContact(1, 420, 3900, true)], signal);
        GestureFeedbackIcon.Kind = "Audio";
        GestureFeedbackTitle.Text = "Left edge · Volume";
        GestureFeedbackValue.Text = "62% · +25%";
        GestureFeedbackOverlay.Opacity = 1;
        GestureStatusText.Text = "Volume · 62% · +25%";
    }

    private void ConfigureResetButton(
        Slider slider,
        TextBlock valueLabel,
        double defaultValue,
        Action<Slider, double> reset,
        string iconKind)
    {
        if (_sliderResetButtons.ContainsKey(slider))
            return;

        Grid? header = FindHeaderBeforeSlider(slider);
        if (header is null)
            return;

        AddSettingIcon(header, valueLabel, iconKind);

        var button = new Button
        {
            Content = new PackIconLucide
            {
                Kind = "Reset",
                Width = 14,
                Height = 14
            },
            Style = TryFindResource("TcIconButton") as Style,
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            Margin = new Thickness(6, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = $"Reset to default ({FormatDefault(defaultValue, slider)})",
            Tag = defaultValue,
            Visibility = Visibility.Hidden
        };
        button.SetResourceReference(Control.ForegroundProperty, "Tc.TextMuted");
        button.Click += (_, _) => reset(slider, defaultValue);

        // Reserve a dedicated right-hand slot beside the entire track. Hidden reset
        // buttons retain that space, so appearing/disappearing never crowds the
        // value label or changes the slider width.
        FrameworkElement trackHost = slider.Parent is Grid sliderOverlay ? sliderOverlay : slider;
        if (trackHost.Parent is StackPanel stack)
        {
            int index = stack.Children.IndexOf(trackHost);
            Thickness margin = trackHost.Margin;
            trackHost.Margin = new Thickness(0);
            stack.Children.RemoveAt(index);

            var row = new Grid { Margin = margin };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            row.Children.Add(trackHost);
            Grid.SetColumn(button, 1);
            row.Children.Add(button);
            stack.Children.Insert(index, row);
        }

        _sliderResetButtons[slider] = button;
    }

    private void AddSettingIcon(Grid header, TextBlock valueLabel, string iconKind)
    {
        TextBlock? label = header.Children
            .OfType<TextBlock>()
            .FirstOrDefault(text => !ReferenceEquals(text, valueLabel));
        if (label is null || label.Tag as string == "ThinkControl.IconizedSettingLabel")
            return;

        int column = Grid.GetColumn(label);
        int row = Grid.GetRow(label);
        int columnSpan = Grid.GetColumnSpan(label);
        int rowSpan = Grid.GetRowSpan(label);
        int index = header.Children.IndexOf(label);
        header.Children.Remove(label);

        var icon = new PackIconLucide
        {
            Kind = iconKind,
            Width = 12,
            Height = 12,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        icon.SetResourceReference(Control.ForegroundProperty, "Tc.TextMuted");

        label.Tag = "ThinkControl.IconizedSettingLabel";
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = label.HorizontalAlignment
        };
        panel.Children.Add(icon);
        panel.Children.Add(label);
        Grid.SetColumn(panel, column);
        Grid.SetRow(panel, row);
        Grid.SetColumnSpan(panel, columnSpan);
        Grid.SetRowSpan(panel, rowSpan);
        header.Children.Insert(Math.Min(index, header.Children.Count), panel);
    }

    private static Grid? FindHeaderBeforeSlider(Slider slider)
    {
        FrameworkElement anchor = slider;
        if (slider.Parent is Grid wrapper && wrapper.Parent is StackPanel)
            anchor = wrapper;
        if (anchor.Parent is not StackPanel stack)
            return null;
        int index = stack.Children.IndexOf(anchor);
        for (int i = index - 1; i >= 0; i--)
        {
            if (stack.Children[i] is Grid grid)
                return grid;
            if (stack.Children[i] is Border)
                break;
        }
        return null;
    }

    private void ResetGestureSlider(Slider slider, double value)
    {
        slider.Value = value;
        CommitGestureSettings();
        RefreshValueFeedback();
    }

    private void ResetHapticSlider(Slider slider, double value)
    {
        if (_host is null)
            return;

        int integer = (int)Math.Round(value);
        bool changed = ReferenceEquals(slider, HapticStrengthSlider)
            ? _host.SetHapticIntensity(integer)
            : _host.SetClickForceSensitivity(integer);
        if (changed)
            slider.Value = integer;
        else
            SyncHaptics();
        RefreshSliderResetButtons();
    }

    private void ResetOsdOpacity(Slider slider, double value)
    {
        slider.Value = value;
        if (_app is not null)
            _app.UserSettings.Update(settings => settings with { TouchpadOsdOpacity = value / 100d });
        RefreshSliderResetButtons();
    }

    private void RefreshValueFeedback()
    {
        RefreshSettingLabels();
        RefreshSliderResetButtons();
    }

    private void RefreshSettingLabels()
    {
        EdgeWidthValue.Text = FormatSetting(EdgeWidthSlider.Value, 5.0, "mm", 1);
        ActivationValue.Text = FormatSetting(ActivationSlider.Value, 2.0, "mm", 1);
        ToleranceValue.Text = FormatSetting(ToleranceSlider.Value, 12.0, "mm", 1);
        SensitivityValue.Text = FormatSetting(SensitivitySlider.Value, 1.0, "×", 1, unitBeforeValue: false);
    }

    private void RefreshSliderResetButtons()
    {
        foreach ((Slider slider, Button button) in _sliderResetButtons)
        {
            double defaultValue = button.Tag is double value ? value : Convert.ToDouble(button.Tag);
            button.Visibility = Math.Abs(slider.Value - defaultValue) >= 0.001
                ? Visibility.Visible
                : Visibility.Hidden;
        }
    }

    private void UpdateGestureValueFeedback(GestureSignal signal)
    {
        if (!IsVisible)
            return;

        if (signal.Phase == GesturePhase.Claimed)
            _gestureStartValue = ReadGestureStartValue(signal.Action);

        if (signal.Phase is GesturePhase.Claimed or GesturePhase.Active)
        {
            StopGestureFeedbackFade();
            GestureFeedbackIcon.Kind = FeedbackIcon(signal.Action);
            GestureFeedbackTitle.Text = $"{EdgeLabel(signal.Edge)} · {ActionLabel(signal.Action)}";
            GestureFeedbackValue.Text = FormatGestureValue(signal);
            GestureFeedbackOverlay.Opacity = 1;
            return;
        }

        if (signal.Phase == GesturePhase.Released)
        {
            GestureFeedbackIcon.Kind = FeedbackIcon(signal.Action);
            GestureFeedbackTitle.Text = ActionLabel(signal.Action);
            GestureFeedbackValue.Text = FormatGestureValue(signal);
            StartGestureFeedbackFade();
            // Keep the captured start value through the release/fade so the final
            // overlay and status line retain the real delta. The next Claimed event
            // replaces it, and page hide/unload clears it.
            return;
        }

        if (signal.Phase == GesturePhase.Cancelled)
        {
            GestureFeedbackIcon.Kind = "Touchpad";
            GestureFeedbackTitle.Text = "Gesture cancelled";
            GestureFeedbackValue.Text = string.IsNullOrWhiteSpace(signal.Reason) ? "Not claimed" : signal.Reason;
            StartGestureFeedbackFade(250, 500);
            _gestureStartValue = null;
        }
    }

    private string FormatGestureStatus(GestureSignal signal) =>
        $"{ActionLabel(signal.Action)} · {FormatGestureValue(signal)}";

    private string FormatGestureValue(GestureSignal signal)
    {
        if (signal.Action == GestureActionKind.MediaSeek && _host is not null)
        {
            double seconds = _host.CurrentSeekDeltaSeconds;
            return $"{seconds:+0.0;-0.0;0.0} s";
        }

        int? current = ReadGestureTargetValue(signal.Action);
        if (current.HasValue)
        {
            int change = _gestureStartValue.HasValue ? current.Value - _gestureStartValue.Value : 0;
            return $"{current.Value}% · {change:+0;-0;0}%";
        }

        if (_gestureStartValue.HasValue && signal.Action is GestureActionKind.Volume or GestureActionKind.Brightness)
            return $"{_gestureStartValue.Value}%";

        if (signal.Action is GestureActionKind.PreviousNextTrack or GestureActionKind.PlayPause or GestureActionKind.Mute or
            GestureActionKind.TaskView or GestureActionKind.ShowDesktop or GestureActionKind.KeyboardBacklight or
            GestureActionKind.PerformanceMode or GestureActionKind.CustomShortcut)
            return "Triggered";

        return $"{signal.TotalTravelMm:+0.0;-0.0;0.0} mm";
    }

    private int? ReadGestureStartValue(GestureActionKind action)
    {
        if (action == GestureActionKind.Volume && _host is not null)
            return Math.Clamp(_host.ReadVolumePercent(), 0, 100);

        if (action == GestureActionKind.Brightness && _app is not null)
            return Math.Clamp(_app.State.Brightness, 0, 100);

        return null;
    }

    private int? ReadGestureTargetValue(GestureActionKind action)
    {
        if (action == GestureActionKind.Volume && _host is not null)
            return _host.CurrentVolumeTarget;

        if (action == GestureActionKind.Brightness && _host is not null)
            return _host.CurrentBrightnessTarget;

        return null;
    }

    private void StopGestureFeedbackFade()
    {
        GestureFeedbackOverlay.BeginAnimation(UIElement.OpacityProperty, null);
    }

    private void StartGestureFeedbackFade(int holdMilliseconds = 450, int fadeMilliseconds = 700)
    {
        StopGestureFeedbackFade();
        GestureFeedbackOverlay.Opacity = 1;
        var fade = new DoubleAnimation
        {
            From = 1,
            To = 0,
            BeginTime = TimeSpan.FromMilliseconds(holdMilliseconds),
            Duration = new Duration(TimeSpan.FromMilliseconds(fadeMilliseconds)),
            FillBehavior = FillBehavior.Stop
        };
        fade.Completed += (_, _) =>
        {
            GestureFeedbackOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            GestureFeedbackOverlay.Opacity = 0;
        };
        GestureFeedbackOverlay.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
    }

    private void ClearGestureFeedback()
    {
        _gestureStartValue = null;
        StopGestureFeedbackFade();
        GestureFeedbackOverlay.Opacity = 0;
    }

    private static string FeedbackIcon(GestureActionKind action) => action switch
    {
        GestureActionKind.Volume or GestureActionKind.MediaSeek or GestureActionKind.PreviousNextTrack or
            GestureActionKind.PlayPause or GestureActionKind.Mute => "Audio",
        GestureActionKind.Brightness => "Monitor",
        GestureActionKind.KeyboardBacklight => "Keyboard",
        GestureActionKind.PerformanceMode => "Gauge",
        GestureActionKind.TaskView or GestureActionKind.ShowDesktop => "Laptop",
        _ => "Touchpad"
    };

    private static string FormatSetting(
        double value,
        double defaultValue,
        string unit,
        int decimals,
        bool unitBeforeValue = false)
    {
        string format = decimals == 2 ? "0.00" : "0.0";
        string current = unitBeforeValue
            ? $"{unit}{value.ToString(format)}"
            : $"{value.ToString(format)}{(unit == "×" ? string.Empty : " ")}{unit}";

        // Tuning controls show the actual setting only. Delta text such as +0.2 is
        // useful during a live gesture, not while configuring a persistent slider.
        return Math.Abs(value - defaultValue) < Math.Pow(10, -decimals) / 2d
            ? $"{current} · default"
            : current;
    }

    private string FormatDefault(double value, Slider slider)
    {
        if (ReferenceEquals(slider, SensitivitySlider))
            return $"{value:0.0}×";
        if (ReferenceEquals(slider, EdgeWidthSlider) || ReferenceEquals(slider, ActivationSlider) || ReferenceEquals(slider, ToleranceSlider))
            return $"{value:0.0} mm";
        if (ReferenceEquals(slider, OsdOpacitySlider))
            return $"{value:0}%";
        return $"{value:0}";
    }
}
