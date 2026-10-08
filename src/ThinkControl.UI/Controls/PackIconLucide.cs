using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace ThinkControl.UI.Controls;

/// <summary>Compatibility type name; all static glyphs now come from the selected Fluent SVG assets.</summary>
public sealed class PackIconLucide : System.Windows.Controls.Control
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(string), typeof(PackIconLucide),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    private static readonly Dictionary<string, string> Assets = new(StringComparer.Ordinal)
    {
        ["House"]="imgOfficialFluent20Overview", ["Gauge"]="imgOfficialFluent20Power",
        ["Fan"]="imgMicrosoftFluentTemperature", ["Monitor"]="imgOfficialFluent20Display",
        ["Audio"]="imgOfficialFluent20Audio", ["Tune"]="imgOfficialFluent20Modes",
        ["Battery"]="imgOfficialFluent20Battery", ["BatteryHorizontal"]="imgOfficialFluent20Battery",
        ["BatteryChargingHorizontal"]="imgOfficialFluent20Battery", ["Lightning"]="flash",
        ["Laptop"]="imgOfficialFluent20System", ["Settings"]="imgOfficialFluent20Settings",
        ["Notifications"]="imgOfficialFluent20Notify", ["Close"]="imgOfficialFluent20Close",
        ["OpenInFull"]="imgOfficialFluent20Expand", ["CompactView"]="imgOfficialFluentWindow",
        ["FullView"]="imgOfficialFluentWindow", ["ViewSidebar"]="imgOfficialFluentWindow",
        ["More"]="imgOfficialFluent20More", ["ChevronDown"]="imgIconFluentChevronDown",
        ["ChevronUp"]="imgIconFluentChevronDown", ["ChevronLeft"]="imgIconFluentChevronDown",
        ["ChevronRight"]="imgIconFluentChevronDown",
        ["Keyboard"]="keyboard", ["Touchpad"]="touchpad", ["Automation"]="branch",
        ["RefreshCw"]="arrow_clockwise", ["Reset"]="arrow_reset", ["Sensors"]="pulse",
        ["Cpu"]="chip", ["Brightness"]="brightness_high", ["Check"]="checkmark", ["Error"]="error_circle",
        ["Play"]="play", ["Pause"]="pause",
        ["Tc.Icon.Audio"]="imgOfficialFluent20Audio", ["Tc.Icon.AudioMuted"]="speaker_mute",
        ["Tc.Icon.AudioLow"]="speaker_1", ["Tc.Icon.Brightness"]="brightness_high",
        ["Tc.Icon.SkipPrevious"]="previous", ["Tc.Icon.SkipNext"]="next",
        ["Tc.Icon.PlayPause"]="play", ["Tc.Icon.SeekBackward"]="previous",
        ["Tc.Icon.SeekForward"]="next", ["Tc.Icon.MediaScrub"]="arrow_bidirectional_left_right",
        ["Tc.Icon.ViewSidebar"]="imgOfficialFluentWindow", ["Tc.Icon.OpenInFull"]="imgOfficialFluent20Expand",
        ["Tc.Icon.Close"]="imgOfficialFluent20Close"
    };

    internal static DrawingBrush? MaskForKind(string kind) =>
        Assets.TryGetValue(kind, out string? name) ? SvgAssetDrawing.Mask(name) : null;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0 || !Assets.TryGetValue(Kind, out string? name)) return;
        DrawingBrush mask = SvgAssetDrawing.Mask(name);
        double angle = Kind switch { "ChevronUp" => 180, "ChevronLeft" => 90, "ChevronRight" => -90, _ => 0 };
        dc.PushTransform(new RotateTransform(angle, ActualWidth / 2, ActualHeight / 2));
        dc.PushOpacityMask(mask);
        dc.DrawRectangle(Foreground, null, new Rect(0, 0, ActualWidth, ActualHeight));
        dc.Pop();
        dc.Pop();
    }
}

/// <summary>Loads untouched packaged SVGs once. Viewport dimensions preserve the asset's original whitespace.</summary>
internal static class SvgAssetDrawing
{
    private static readonly Dictionary<string, DrawingBrush> Masks = new(StringComparer.Ordinal);
    internal static DrawingBrush Mask(string name)
        => Brush(name, new Rect(0, 0, 20, 20));
    internal static DrawingBrush Brush(string name, Rect viewport)
    {
        string key = name + viewport.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (Masks.TryGetValue(key, out DrawingBrush? cached)) return cached;
        var uri = new Uri($"pack://application:,,,/ThinkControl.UI;component/Assets/Figma/{name}.svg");
        using Stream stream = System.Windows.Application.GetResourceStream(uri).Stream;
        var reader = new FileSvgReader(new WpfDrawingSettings { IncludeRuntime = false, TextAsGeometry = true });
        DrawingGroup drawing = reader.Read(stream);
        var brush = new DrawingBrush(drawing)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = viewport,
            Stretch = Stretch.Fill
        };
        brush.Freeze();
        Masks[key] = brush;
        return brush;
    }
}
