using System.Windows;
using System.Windows.Controls;
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
        ConfigureResetButton(ToleranceSlider, ToleranceValue, 12.0, ResetGestureSlider, "Tune");
        ConfigureResetButton(SensitivitySlider, SensitivityValue, 1.0, ResetGestureSlider, "Tune");
        ConfigureResetButton(HapticStrengthSlider, HapticStrengthValue, App.DefaultHapticFeedbackIntensity, ResetHapticSlider, "Touchpad");
        ConfigureResetButton(ClickForceSlider, ClickForceValue, App.DefaultHapticClickSensitivity, ResetHapticSlider, "Touchpad");
        ConfigureResetButton(OsdOpacitySlider, OsdOpacityValue, 92.0, ResetOsdOpacity, "Monitor");
        RefreshValueFeedback();
    }

    internal void PrepareForSnapshot(bool showActiveGesture, bool showInwardGesture = false)
    {
        _settingsSaveTimer.Stop();
        ClearGestureFeedback();
        InputStatusText.Text = "Precision Touchpad detected";

        if (!showActiveGesture && !showInwardGesture)
        {
            PrepareSnapshotBinding(TouchpadEdge.Top, GestureActionKind.MediaSeek, trackCenter: false);
            Visualizer.SetTestFrame(Array.Empty<TouchContact>(), null);
            return;
        }

        if (showInwardGesture)
        {
            PrepareSnapshotBinding(TouchpadEdge.Right, GestureActionKind.OpenThinkControl, trackCenter: false);
            var inward = new GestureSignal(
                GesturePhase.Active,
                TouchpadEdge.Right,
                GestureActionKind.OpenThinkControl,
                TotalTravelMm: 8.4,
                DeltaMm: 2.2,
                ContactId: 1);
            Visualizer.SetTestFrame([new TouchContact(1, 9600, 1200, true)], inward);
            Visualizer.SetTestFrame([new TouchContact(1, 7800, 3100, true)], inward);
            Visualizer.ShowActiveGestureValue(TouchpadEdge.Right, "Compact view");
            GestureStatusText.Text = "Open Compact · inward";
            return;
        }

        // Wide QA explicitly covers Track control plus its optional center action.
        PrepareSnapshotBinding(TouchpadEdge.Bottom, GestureActionKind.PreviousNextTrack, trackCenter: true);

        var signal = new GestureSignal(
            GesturePhase.Active,
            TouchpadEdge.Bottom,
            GestureActionKind.PreviousNextTrack,
            TotalTravelMm: 7.2,
            DeltaMm: 1.4,
            ContactId: 1);

        Visualizer.SetTestFrame([new TouchContact(1, 5300, 7700, true)], signal);
        Visualizer.SetTestFrame([new TouchContact(1, 8500, 7700, true)], signal);
        Visualizer.ShowActiveGestureValue(TouchpadEdge.Bottom, "Next");
        GestureStatusText.Text = "Track control · Next";
    }

    private void PrepareSnapshotBinding(TouchpadEdge selectedEdge, GestureActionKind action, bool trackCenter)
    {
        _syncing = true;
        try
        {
            _selectedEdge = selectedEdge;
            GestureEnableSwitch.IsChecked = true;
            TouchpadGestureBindings bindings = _configuration.Bindings ?? TouchpadGestureBindings.AsusStyle;
            foreach (TouchpadEdge edge in Enum.GetValues<TouchpadEdge>())
            {
                if (edge == selectedEdge)
                    continue;
                TouchpadEdgeBinding existing = bindings.Get(edge).Sanitize();
                if (existing.Action == action)
                    bindings = WithBinding(bindings, edge, existing with { Action = GestureActionKind.Disabled });
            }

            _configuration = (_configuration with
            {
                Enabled = true,
                TrackCenterPlayPauseEnabled = trackCenter,
                Bindings = WithBinding(bindings, selectedEdge, new TouchpadEdgeBinding(action))
            }).Sanitize();
            Visualizer.SelectedEdge = _selectedEdge;
            Visualizer.Configuration = _configuration;
            SyncSelectedEdge();
            SyncTrackCenterOption();
        }
        finally
        {
            _syncing = false;
        }
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
            Content = new PackIconLucide { Kind = "Reset", Width = 14, Height = 14 },
            Style = TryFindResource("TcIconButton") as Style,
            Width = 24,
            Height = 24,
            Padding = new Thickness(0),
            Margin = new Thickness(5, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = $"Reset to default ({FormatDefault(defaultValue, slider)})",
            Tag = defaultValue,
            Visibility = Visibility.Hidden
        };
        button.SetResourceReference(Control.ForegroundProperty, "Tc.TextMuted");
        button.Click += (_, _) => reset(slider, defaultValue);

        if (valueLabel.Parent is Grid valueHeader)
        {
            int column = Grid.GetColumn(valueLabel);
            int row = Grid.GetRow(valueLabel);
            int columnSpan = Grid.GetColumnSpan(valueLabel);
            int rowSpan = Grid.GetRowSpan(valueLabel);
            valueHeader.Children.Remove(valueLabel);
            valueLabel.Margin = new Thickness(0);
            var trailing = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            trailing.Children.Add(valueLabel);
            trailing.Children.Add(button);
            Grid.SetColumn(trailing, column);
            Grid.SetRow(trailing, row);
            Grid.SetColumnSpan(trailing, columnSpan);
            Grid.SetRowSpan(trailing, rowSpan);
            valueHeader.Children.Add(trailing);
        }
        else
        {
            header.Children.Add(button);
        }

        _sliderResetButtons[slider] = button;
    }

    private void AddSettingIcon(Grid header, TextBlock valueLabel, string iconKind)
    {
        TextBlock? label = header.Children.OfType<TextBlock>().FirstOrDefault(text => !ReferenceEquals(text, valueLabel));
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
        if (changed) slider.Value = integer; else SyncHaptics();
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
        EdgeWidthValue.Text = FormatSetting(EdgeWidthSlider.Value, "mm", 1);
        ActivationValue.Text = FormatSetting(ActivationSlider.Value, "mm", 1);
        ToleranceValue.Text = FormatSetting(ToleranceSlider.Value, "mm", 1);
        SensitivityValue.Text = FormatSetting(SensitivitySlider.Value, "x", 1);
    }

    private void RefreshSliderResetButtons()
    {
        foreach ((Slider slider, Button button) in _sliderResetButtons)
        {
            double defaultValue = button.Tag is double value ? value : Convert.ToDouble(button.Tag);
            button.Visibility = Math.Abs(slider.Value - defaultValue) >= 0.001 ? Visibility.Visible : Visibility.Hidden;
        }
    }

    private void UpdateGestureValueFeedback(GestureSignal signal)
    {
        if (!IsVisible)
            return;

        if (signal.Phase == GesturePhase.Candidate)
        {
            if (signal.Action == GestureActionKind.PreviousNextTrack && _configuration.TrackCenterPlayPauseEnabled)
                Visualizer.ShowActiveGestureValue(signal.Edge, "Hold · Play / Pause");
            return;
        }

        if (signal.Phase == GesturePhase.Claimed)
            _gestureStartValue = ReadGestureStartValue(signal.Action);

        if (signal.Phase is GesturePhase.Claimed or GesturePhase.Active)
        {
            Visualizer.ClearReleasedGestureFeedback();
            Visualizer.ShowActiveGestureValue(signal.Edge, FormatGestureValue(signal));
            return;
        }

        if (signal.Phase == GesturePhase.Released)
        {
            Visualizer.ClearActiveGestureFeedback();
            string value = FormatGestureValue(signal);
            if (!(signal.Action == GestureActionKind.PreviousNextTrack && Math.Abs(signal.TotalTravelMm) < 0.5))
                Visualizer.ShowReleasedGestureValue(signal.Edge, value);
            _gestureStartValue = null;
            return;
        }

        if (signal.Phase == GesturePhase.Cancelled)
        {
            Visualizer.ClearActiveGestureFeedback();
            Visualizer.ClearReleasedGestureFeedback();
            _gestureStartValue = null;
        }
    }

    private string FormatGestureStatus(GestureSignal signal)
    {
        if (signal.Phase is GesturePhase.Claimed or GesturePhase.Active)
            return $"{ActionLabel(signal.Action)} · {FormatGestureDirection(signal)}";
        return $"{ActionLabel(signal.Action)} · {FormatGestureValue(signal)}";
    }

    private string FormatGestureDirection(GestureSignal signal)
    {
        bool vertical = signal.Edge is TouchpadEdge.Left or TouchpadEdge.Right;
        bool positive = vertical ? signal.DeltaMm < 0 : signal.DeltaMm > 0;
        return positive ? "+" : "−";
    }

    private string FormatGestureValue(GestureSignal signal)
    {
        if (_host is null)
            return "Complete";
        return signal.Action switch
        {
            GestureActionKind.Volume => $"{ResolveCurrentPercent(_host.CurrentVolumeTarget, _host.ReadVolumePercent())}%",
            GestureActionKind.Brightness => $"{ResolveCurrentPercent(_host.CurrentBrightnessTarget, _app?.State.Brightness ?? 0)}%",
            GestureActionKind.MediaSeek => FormatSeekDelta(_host.CurrentSeekDeltaSeconds),
            GestureActionKind.PreviousNextTrack => Math.Abs(signal.TotalTravelMm) < 0.5
                ? "Play / Pause"
                : signal.TotalTravelMm >= 0 ? "Next" : "Previous",
            GestureActionKind.PlayPause => "Play / Pause",
            GestureActionKind.OpenThinkControl => "Compact view",
            _ => "Complete"
        };
    }

    private int? ReadGestureStartValue(GestureActionKind action)
    {
        if (_host is null)
            return null;
        return action switch
        {
            GestureActionKind.Volume => _host.ReadVolumePercent(),
            GestureActionKind.Brightness => _app?.State.Brightness,
            _ => null
        };
    }

    private int ResolveCurrentPercent(int? queuedTarget, int fallback)
    {
        int value = queuedTarget ?? fallback;
        return Math.Clamp(value, 0, 100);
    }

    private static string FormatSeekDelta(double seconds)
    {
        if (Math.Abs(seconds) < 0.5)
            return "0 s";
        string sign = seconds > 0 ? "+" : "−";
        return $"{sign}{Math.Abs(seconds):0.#} s";
    }

    private void ClearGestureFeedback()
    {
        _gestureStartValue = null;
        Visualizer.ClearActiveGestureFeedback();
        Visualizer.ClearReleasedGestureFeedback();
    }

    private static string FormatSetting(double value, string suffix, int decimals)
    {
        string format = decimals <= 0 ? "0" : "0." + new string('0', decimals);
        return $"{value.ToString(format, System.Globalization.CultureInfo.InvariantCulture)}{suffix}";
    }

    private static string FormatDefault(double value, Slider slider)
    {
        if (ReferenceEquals(slider, HapticStrengthSliderPlaceholder) || ReferenceEquals(slider, ClickForceSliderPlaceholder))
            return $"{value:0}%";
        return value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }

    // These placeholders are never instantiated. They keep FormatDefault free of
    // instance state while preserving a concise tooltip format for the simple sliders.
    private static readonly Slider HapticStrengthSliderPlaceholder = new();
    private static readonly Slider ClickForceSliderPlaceholder = new();
}
