using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ThinkControl.UI.Services;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;
using WpfApplication = System.Windows.Application;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class GestureOsdService : IDisposable
{
    private readonly Func<ThinkControlUserSettings> _settings;
    private readonly Func<string, int, bool> _setValue;
    private readonly Func<bool> _toggleMute;
    private readonly DispatcherTimer _hideTimer;
    private Window? _window;
    private Border? _shell;
    private Button? _iconButton;
    private Path? _iconPath;
    private TextBlock? _label;
    private TextBlock? _value;
    private Slider? _slider;
    private bool _syncing;
    private string _activeLabel = string.Empty;

    internal GestureOsdService(
        Func<ThinkControlUserSettings> settings,
        Func<string, int, bool> setValue,
        Func<bool> toggleMute)
    {
        _settings = settings;
        _setValue = setValue;
        _toggleMute = toggleMute;
        _hideTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(1250)
        };
        _hideTimer.Tick += (_, _) => Hide();
    }

    internal void Show(string label, int value)
    {
        ThinkControlUserSettings settings = _settings();
        if (!settings.TouchpadOsdEnabled)
            return;

        EnsureWindow();
        if (_window is null || _label is null || _value is null || _slider is null || _shell is null || _iconPath is null || _iconButton is null)
            return;

        _activeLabel = label;
        int clamped = Math.Clamp(value, 0, 100);
        bool volume = label.Contains("Volume", StringComparison.OrdinalIgnoreCase) || label.Contains("Muted", StringComparison.OrdinalIgnoreCase);
        bool brightness = label.Contains("Brightness", StringComparison.OrdinalIgnoreCase);

        _syncing = true;
        try
        {
            _label.Text = label.Contains("Muted", StringComparison.OrdinalIgnoreCase) ? "Volume" : label;
            _value.Text = label.Contains("Muted", StringComparison.OrdinalIgnoreCase) ? "Muted" : $"{clamped}%";
            _slider.Value = clamped;
            _slider.IsEnabled = volume || brightness;
            _iconButton.IsEnabled = volume;
            _iconButton.ToolTip = volume ? "Mute / unmute" : null;
            _iconPath.Data = Geometry.Parse(volume ? VolumeGeometry : BrightnessGeometry);
        }
        finally
        {
            _syncing = false;
        }

        _window.Opacity = Math.Clamp(settings.TouchpadOsdOpacity, 0.65, 1.0);
        Rect area = SystemParameters.WorkArea;
        double targetLeft = settings.TouchpadOsdPosition switch
        {
            "Left" => area.Left + 24,
            "Right" => area.Right - _window.Width - 24,
            _ => area.Left + (area.Width - _window.Width) / 2d
        };
        double targetTop = area.Bottom - _window.Height - 16;
        _window.Left = targetLeft;

        if (!_window.IsVisible)
        {
            // Start just below the usable desktop so the card rises from the
            // taskbar edge rather than appearing detached above it.
            double startTop = area.Bottom + 6;
            _window.Top = startTop;
            _window.Show();
            if (SystemParameters.ClientAreaAnimation)
            {
                _window.BeginAnimation(Window.TopProperty, new DoubleAnimation(startTop, targetTop, TimeSpan.FromMilliseconds(165))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
            }
            else
            {
                _window.Top = targetTop;
            }
        }
        else
        {
            _window.BeginAnimation(Window.TopProperty, null);
            _window.Top = targetTop;
        }

        RestartHideTimer();
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

        _iconPath = new Path
        {
            Width = 18,
            Height = 18,
            Stretch = Stretch.Uniform,
            Stroke = text,
            StrokeThickness = 1.65,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Data = Geometry.Parse(VolumeGeometry)
        };
        _iconButton = new Button
        {
            Width = 38,
            Height = 38,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = _iconPath,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        _iconButton.Click += (_, _) =>
        {
            if (_activeLabel.Contains("Volume", StringComparison.OrdinalIgnoreCase) || _activeLabel.Contains("Muted", StringComparison.OrdinalIgnoreCase))
                _toggleMute();
            RestartHideTimer();
        };

        _label = new TextBlock
        {
            FontFamily = new MediaFontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = text,
            VerticalAlignment = VerticalAlignment.Center
        };
        _value = new TextBlock
        {
            FontFamily = new MediaFontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 11,
            Foreground = muted,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        var header = new Grid { Margin = new Thickness(0, 1, 0, 5) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(_label);
        Grid.SetColumn(_value, 1);
        header.Children.Add(_value);

        _slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Height = 24,
            IsMoveToPointEnabled = true,
            SmallChange = 1,
            LargeChange = 5,
            Style = WpfApplication.Current?.TryFindResource("TcSlider") as Style
        };
        _slider.ValueChanged += (_, e) =>
        {
            if (_syncing || !_slider.IsMouseCaptureWithin)
                return;
            int value = (int)Math.Round(e.NewValue);
            if (_setValue(_activeLabel, value))
                _value!.Text = $"{value}%";
            RestartHideTimer();
        };
        _slider.PreviewMouseDown += (_, _) => _hideTimer.Stop();
        _slider.PreviewMouseUp += (_, _) => RestartHideTimer();

        var right = new Grid { Margin = new Thickness(10, 0, 0, 0) };
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.Children.Add(header);
        Grid.SetRow(_slider, 1);
        right.Children.Add(_slider);

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        content.ColumnDefinitions.Add(new ColumnDefinition());
        content.Children.Add(_iconButton);
        Grid.SetColumn(right, 1);
        content.Children.Add(right);

        _shell = new Border
        {
            Background = surface,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(11, 9, 13, 9),
            Child = content
        };
        _shell.MouseEnter += (_, _) => _hideTimer.Stop();
        _shell.MouseLeave += (_, _) => RestartHideTimer();

        _window = new Window
        {
            Width = 286,
            Height = 72,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Content = _shell
        };
    }

    private void RestartHideTimer()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void Hide()
    {
        _hideTimer.Stop();
        if (_window is null || !_window.IsVisible)
            return;

        if (!SystemParameters.ClientAreaAnimation)
        {
            _window.Hide();
            return;
        }

        Rect area = SystemParameters.WorkArea;
        double from = _window.Top;
        double to = area.Bottom + 5;
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(125))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        animation.Completed += (_, _) =>
        {
            if (_window is null)
                return;
            _window.Hide();
            _window.BeginAnimation(Window.TopProperty, null);
        };
        _window.BeginAnimation(Window.TopProperty, animation);
    }

    private static Brush BrushResource(string key, Brush fallback) =>
        WpfApplication.Current?.TryFindResource(key) as Brush ?? fallback;

    public void Dispose()
    {
        _hideTimer.Stop();
        try { _window?.Close(); } catch { }
        _window = null;
    }

    private const string VolumeGeometry = "M2,7 L5,7 L9,4 L9,14 L5,11 L2,11 Z M12,7 C13.5,8 13.5,10 12,11 M14,5 C17,7 17,11 14,13";
    private const string BrightnessGeometry = "M9,2 L9,4 M9,14 L9,16 M2,9 L4,9 M14,9 L16,9 M4,4 L5.5,5.5 M12.5,12.5 L14,14 M14,4 L12.5,5.5 M5.5,12.5 L4,14 M9,6 A3,3 0 1 0 9,12 A3,3 0 1 0 9,6";
}
