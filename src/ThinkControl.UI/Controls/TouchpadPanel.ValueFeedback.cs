using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Touchpad;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class TouchpadPanel
{
    private readonly WindowsVolumeService _gestureVolume = new();
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

        ConfigureResetButton(EdgeWidthSlider, EdgeWidthValue, 5.0, ResetGestureSlider);
        ConfigureResetButton(ActivationSlider, ActivationValue, 2.0, ResetGestureSlider);
        ConfigureResetButton(ToleranceSlider, ToleranceValue, 12.0, ResetGestureSlider);
        ConfigureResetButton(SensitivitySlider, SensitivityValue, 1.0, ResetGestureSlider);
        ConfigureResetButton(HapticStrengthSlider, HapticStrengthValue, App.DefaultHapticFeedbackIntensity, ResetHapticSlider);
        ConfigureResetButton(ClickForceSlider, ClickForceValue, App.DefaultHapticClickSensitivity, ResetHapticSlider);
        ConfigureResetButton(OsdOpacitySlider, OsdOpacityValue, 92.0, ResetOsdOpacity);

        if (_host is not null)
            _host.GestureChanged += Host_GestureValueFeedback;

        RefreshValueFeedback();
    }

    private void ConfigureResetButton(
        Slider slider,
        TextBlock valueLabel,
        double defaultValue,
        Action<Slider, double> reset)
    {
        if (_sliderResetButtons.ContainsKey(slider))
            return;

        Grid? header = FindHeaderBeforeSlider(slider);
        if (header is null)
            return;

        valueLabel.Margin = new Thickness(valueLabel.Margin.Left, valueLabel.Margin.Top, 27, valueLabel.Margin.Bottom);

        var icon = new PackIconLucide
        {
            Kind = "RefreshCw",
            Width = 11,
            Height = 11
        };
        icon.SetResourceReference(ForegroundProperty, "Tc.TextMuted");

        var button = new Button
        {
            Width = 23,
            Height = 23,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Content = icon,
            ToolTip = $"Reset to default ({FormatDefault(defaultValue, slider)})",
            Tag = defaultValue,
            Style = TryFindResource("TcIconButton") as Style,
            Visibility = Visibility.Collapsed
        };
        button.Click += (_, _) => reset(slider, defaultValue);
        Panel.SetZIndex(button, 4);
        header.Children.Add(button);
        _sliderResetButtons[slider] = button;
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
        RefreshSignedSettingLabels();
        RefreshSliderResetButtons();
    }

    private void RefreshSignedSettingLabels()
    {
        EdgeWidthValue.Text = FormatSetting(EdgeWidthSlider.Value, 5.0, "mm", 1);
        ActivationValue.Text = FormatSetting(ActivationSlider.Value, 2.0, "mm", 1);
        ToleranceValue.Text = FormatSetting(ToleranceSlider.Value, 12.0, "mm", 1);
        SensitivityValue.Text = FormatSetting(SensitivitySlider.Value, 1.0, "×", 2, unitBeforeDelta: true);
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
                _gestureStartValue = ReadCurrentActionValue(signal.Action);

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

        int? current = ReadCurrentActionValue(signal.Action);
        if (current.HasValue)
        {
            int change = _gestureStartValue.HasValue ? current.Value - _gestureStartValue.Value : 0;
            return $"{ActionLabel(signal.Action)} · {current.Value}% · {change:+0;-0;0}%";
        }

        return $"{ActionLabel(signal.Action)} · {signal.TotalTravelMm:+0.0;-0.0;0.0} mm";
    }

    private int? ReadCurrentActionValue(GestureActionKind action)
    {
        if (action == GestureActionKind.Volume)
        {
            WindowsVolumeStatus volume = _gestureVolume.Read();
            return volume.Available ? volume.Percent : null;
        }

        if (action == GestureActionKind.Brightness && _app is not null)
            return Math.Clamp(_app.State.Brightness, 0, 100);

        return null;
    }

    private static string FormatSetting(
        double value,
        double defaultValue,
        string unit,
        int decimals,
        bool unitBeforeDelta = false)
    {
        string format = decimals == 2 ? "0.00" : "0.0";
        double delta = value - defaultValue;
        string current = unitBeforeDelta
            ? $"{value.ToString(format)}{unit}"
            : $"{value.ToString(format)} {unit}";
        if (Math.Abs(delta) < Math.Pow(10, -decimals) / 2d)
            return $"{current} · default";

        string signed = delta.ToString(decimals == 2 ? "+0.00;-0.00;0.00" : "+0.0;-0.0;0.0");
        return unitBeforeDelta
            ? $"{current} · {signed}"
            : $"{current} · {signed} {unit}";
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
