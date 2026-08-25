using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        if (_host is not null)
            _host.GestureChanged += Host_GestureValueFeedback;

        RefreshValueFeedback();
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
            Content = "Reset",
            MinWidth = 0,
            Height = 24,
            Padding = new Thickness(8, 0, 0, 0),
            Margin = new Thickness(8, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 9.5,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = $"Reset to default ({FormatDefault(defaultValue, slider)})",
            Tag = defaultValue,
            Visibility = Visibility.Collapsed
        };
        button.SetResourceReference(Control.ForegroundProperty, "Tc.TextMuted");
        button.Click += (_, _) => reset(slider, defaultValue);

        // Put reset beside the slider instead of in the value header. The previous
        // boxed/history-looking icon made every row look like an undo control and
        // stole width from the actual value. This flat text affordance stays out of
        // the way and appears only when the value differs from default.
        if (slider.Parent is StackPanel stack)
        {
            int index = stack.Children.IndexOf(slider);
            if (index >= 0)
            {
                stack.Children.RemoveAt(index);
                var row = new Grid { Margin = slider.Margin };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                slider.Margin = new Thickness(0);
                row.Children.Add(slider);
                Grid.SetColumn(button, 1);
                row.Children.Add(button);
                stack.Children.Insert(index, row);
            }
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
        if (slider.Parent is not StackPanel stack)
            return null;
        int index = stack.Children.IndexOf(slider);
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
        SensitivityValue.Text = FormatSetting(SensitivitySlider.Value, 1.0, "×", 2, unitBeforeValue: false);
    }

    private void RefreshSliderResetButtons()
    {
        foreach ((Slider slider, Button button) in _sliderResetButtons)
        {
            double defaultValue = button.Tag is double value ? value : Convert.ToDouble(button.Tag);
            button.Visibility = Math.Abs(slider.Value - defaultValue) >= 0.001
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void Host_GestureValueFeedback(GestureSignal signal)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (signal.Phase == GesturePhase.Claimed)
                _gestureStartValue = ReadGestureStartValue(signal.Action);

            if (signal.Phase == GesturePhase.Active)
                GestureStatusText.Text = FormatActiveGestureValue(signal);

            if (signal.Phase is GesturePhase.Released or GesturePhase.Cancelled)
                _gestureStartValue = null;
        });
    }

    private string FormatActiveGestureValue(GestureSignal signal)
    {
        if (signal.Action == GestureActionKind.MediaSeek && _host is not null)
        {
            double seconds = _host.CurrentSeekDeltaSeconds;
            return $"Media seek · {seconds:+0.0;-0.0;0.0} s";
        }

        int? current = ReadGestureTargetValue(signal.Action);
        if (current.HasValue)
        {
            int change = _gestureStartValue.HasValue ? current.Value - _gestureStartValue.Value : 0;
            return $"{ActionLabel(signal.Action)} · {current.Value}% · {change:+0;-0;0}%";
        }

        return $"{ActionLabel(signal.Action)} · {signal.TotalTravelMm:+0.0;-0.0;0.0} mm";
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
            return $"{value:0.00}×";
        if (ReferenceEquals(slider, EdgeWidthSlider) || ReferenceEquals(slider, ActivationSlider) || ReferenceEquals(slider, ToleranceSlider))
            return $"{value:0.0} mm";
        if (ReferenceEquals(slider, OsdOpacitySlider))
            return $"{value:0}%";
        return $"{value:0}";
    }
}
