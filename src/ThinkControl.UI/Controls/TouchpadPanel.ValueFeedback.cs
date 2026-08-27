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
            TotalTravelMm: -24.8,
            DeltaMm: -3.1,
            ContactId: 1);

        Visualizer.SetTestFrame([new TouchContact(1, 420, 6400, true)], signal);
        Visualizer.SetTestFrame([new TouchContact(1, 420, 3900, true)], signal);
        GestureStatusText.Text = "Volume · +";
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
        if (signal.Phase == GesturePhase.Claimed)
            _gestureStartValue = ReadGestureStartValue(signal.Action);
        if (signal.Phase is GesturePhase.Claimed or GesturePhase.Active)
        {
            Visualizer.ClearReleasedGestureFeedback();
            return;
        }
        if (signal.Phase == GesturePhase.Released)
        {
            Visualizer.ShowReleasedGestureValue(signal.Edge, FormatGestureValue(signal));
            _gestureStartValue = null;
            return;
        }
        if (signal.Phase == GesturePhase.Cancelled)
        {
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
        double signed = signal.Edge is TouchpadEdge.Left or TouchpadEdge.Right ? -signal.DeltaMm : signal.DeltaMm;
        if (Math.Abs(signed) < 0.01)
            signed = signal.Edge is TouchpadEdge.Left or TouchpadEdge.Right ? -signal.TotalTravelMm : signal.TotalTravelMm;
        if (signal.Action == GestureActionKind.PreviousNextTrack)
            return signed >= 0 ? "Next" : "Previous";
        return signed >= 0 ? "+" : "−";
    }

    private string FormatGestureValue(GestureSignal signal)
    {
        if (signal.Action == GestureActionKind.MediaSeek && _host is not null)
            return $"{_host.CurrentSeekDeltaSeconds:+0.0;-0.0;0.0} s";

        int? current = ReadGestureTargetValue(signal.Action);
        if (current.HasValue)
            return $"{current.Value}%";
        if (_gestureStartValue.HasValue && signal.Action is GestureActionKind.Volume or GestureActionKind.Brightness)
            return $"{_gestureStartValue.Value}%";

        return signal.Action switch
        {
            GestureActionKind.PreviousNextTrack => FormatGestureDirection(signal) == "Next" ? "Next track" : "Previous track",
            GestureActionKind.PlayPause => "Play / pause",
            GestureActionKind.Mute => "Mute toggled",
            GestureActionKind.TaskView => "Task view",
            GestureActionKind.ShowDesktop => "Desktop toggled",
            GestureActionKind.KeyboardBacklight => "Keyboard level changed",
            GestureActionKind.PerformanceMode => "Performance mode changed",
            GestureActionKind.CustomShortcut => "Shortcut sent",
            _ => $"{signal.TotalTravelMm:+0.0;-0.0;0.0} mm"
        };
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

    private void ClearGestureFeedback()
    {
        _gestureStartValue = null;
        Visualizer.ClearReleasedGestureFeedback();
    }

    private static string FormatSetting(double value, string unit, int decimals)
    {
        string format = decimals == 2 ? "0.00" : "0.0";
        return unit.Equals("x", StringComparison.OrdinalIgnoreCase)
            ? $"{value.ToString(format)}x"
            : $"{value.ToString(format)} {unit}";
    }

    private string FormatDefault(double value, Slider slider)
    {
        if (ReferenceEquals(slider, SensitivitySlider)) return $"{value:0.0}x";
        if (ReferenceEquals(slider, EdgeWidthSlider) || ReferenceEquals(slider, ActivationSlider) || ReferenceEquals(slider, ToleranceSlider)) return $"{value:0.0} mm";
        if (ReferenceEquals(slider, OsdOpacitySlider)) return $"{value:0}%";
        return $"{value:0}";
    }
}
