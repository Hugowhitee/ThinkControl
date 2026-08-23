using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;
using WpfApplication = System.Windows.Application;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class GestureOsdService : IDisposable
{
    private readonly DispatcherTimer _hideTimer;
    private Window? _window;
    private TextBlock? _label;
    private TextBlock? _value;
    private Border? _fill;
    private Grid? _track;
    private int _lastValue;

    internal GestureOsdService()
    {
        _hideTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(950)
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            _window?.Hide();
        };
    }

    internal void Show(string label, int value)
    {
        EnsureWindow();
        if (_window is null || _label is null || _value is null || _fill is null || _track is null)
            return;

        _lastValue = Math.Clamp(value, 0, 100);
        _label.Text = label;
        _value.Text = $"{_lastValue}%";
        UpdateFill();

        Rect area = SystemParameters.WorkArea;
        _window.Left = area.Left + (area.Width - _window.Width) / 2d;
        _window.Top = area.Bottom - _window.Height - 34;
        if (!_window.IsVisible)
            _window.Show();

        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void EnsureWindow()
    {
        if (_window is not null)
            return;

        Brush surface = BrushResource("Tc.Surface", new SolidColorBrush(MediaColor.FromRgb(28, 30, 33)));
        Brush surfaceAlt = BrushResource("Tc.SurfaceAlt", new SolidColorBrush(MediaColor.FromRgb(34, 36, 40)));
        Brush border = BrushResource("Tc.BorderStrong", new SolidColorBrush(MediaColor.FromRgb(70, 75, 82)));
        Brush text = BrushResource("Tc.Text", Brushes.White);
        Brush muted = BrushResource("Tc.TextMuted", Brushes.LightGray);
        Brush accent = BrushResource("Tc.Accent", new SolidColorBrush(MediaColor.FromRgb(227, 41, 41)));

        _label = new TextBlock
        {
            FontFamily = new MediaFontFamily("Segoe UI"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = text,
            VerticalAlignment = VerticalAlignment.Center
        };
        _value = new TextBlock
        {
            FontFamily = new MediaFontFamily("Segoe UI"),
            FontSize = 10.5,
            Foreground = muted,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(_label);
        header.Children.Add(_value);

        _fill = new Border
        {
            Background = accent,
            Height = 3,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(1.5)
        };
        _track = new Grid
        {
            Height = 3,
            Background = surfaceAlt,
            ClipToBounds = true
        };
        _track.Children.Add(_fill);
        _track.SizeChanged += (_, _) => UpdateFill();

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.Children.Add(header);
        Grid.SetRow(_track, 1);
        content.Children.Add(_track);

        var shell = new Border
        {
            Background = surface,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 11),
            Child = content
        };

        _window = new Window
        {
            Width = 224,
            Height = 61,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Content = shell
        };
    }

    private void UpdateFill()
    {
        if (_fill is null || _track is null)
            return;
        _fill.Width = Math.Max(0, _track.ActualWidth * _lastValue / 100d);
    }

    private static Brush BrushResource(string key, Brush fallback) =>
        WpfApplication.Current?.TryFindResource(key) as Brush ?? fallback;

    public void Dispose()
    {
        _hideTimer.Stop();
        try { _window?.Close(); } catch { }
        _window = null;
    }
}
