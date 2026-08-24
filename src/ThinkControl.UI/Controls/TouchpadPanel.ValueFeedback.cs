using ThinkControl.Core.Touchpad;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class TouchpadPanel
{
    private readonly WindowsVolumeService _gestureVolume = new();
    private bool _valueFeedbackConfigured;
    private int? _gestureStartValue;

    internal void ConfigureValueFeedback()
    {
        if (_valueFeedbackConfigured)
            return;
        _valueFeedbackConfigured = true;

        EdgeWidthSlider.ValueChanged += (_, _) => RefreshSignedSettingLabels();
        ActivationSlider.ValueChanged += (_, _) => RefreshSignedSettingLabels();
        ToleranceSlider.ValueChanged += (_, _) => RefreshSignedSettingLabels();
        SensitivitySlider.ValueChanged += (_, _) => RefreshSignedSettingLabels();
        Loaded += (_, _) => RefreshSignedSettingLabels();

        if (_host is not null)
            _host.GestureChanged += Host_GestureValueFeedback;

        RefreshSignedSettingLabels();
    }

    private void RefreshSignedSettingLabels()
    {
        EdgeWidthValue.Text = FormatSetting(EdgeWidthSlider.Value, 5.0, "mm", 1);
        ActivationValue.Text = FormatSetting(ActivationSlider.Value, 2.0, "mm", 1);
        ToleranceValue.Text = FormatSetting(ToleranceSlider.Value, 12.0, "mm", 1);
        SensitivityValue.Text = FormatSetting(SensitivitySlider.Value, 1.0, "×", 2, unitBeforeDelta: true);
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
}
