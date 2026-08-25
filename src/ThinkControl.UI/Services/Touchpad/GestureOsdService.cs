using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ThinkControl.UI.Services;
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
    private TranslateTransform? _shellTransform;
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
            Interval = TimeSpan.FromMilliseconds(1180)
        };
        _hideTimer.Tick += (_, _) => Hide();
    }

    internal void Show(string label, int value)
    {
        ThinkControlUserSettings settings = _settings();
        if (!settings.TouchpadOsdEnabled)
            return;

        EnsureWindow();
        if (_window is null || _label is null || _value is null || _slider is null || _shell is null ||
            _shellTransform is null || _iconPath is null || _iconButton is null)
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
            _iconButton.ToolTip = volume ? "Mute / unmute" : brightness ? "Brightness" : null;
            _iconPath.Data = Geometry.Parse(volume ? VolumeGeometry : BrightnessGeometry);
        }
        finally
        {
            _syncing = false;
        }

        _shell.Opacity = Math.Clamp(settings.TouchpadOsdOpacity, 0.65, 1.0);
        _window.Opacity = 1;

        Rect area = SystemParameters.WorkArea;
        double targetLeft = settings.TouchpadOsdPosition switch
        {
            "Left" => area.Left + 24,
            "Right" => area.Right - _window.Width - 24,
            _ => area.Left + (area.Width - _window.Width) / 2d
        };
        double targetTop = area.Bottom - _window.Height - 14;
        _window.Left = targetLeft;
        _window.Top = targetTop;

        if (!_window.IsVisible)
        {
            _shellTransform.BeginAnimation(TranslateTransform.YProperty, null);
            _shellTransform.Y = _window.Height + 10;
            _window.Show();
            if (SystemParameters.ClientAreaAnimation)
            {
                _shellTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(
                    _window.Height + 10,
                    0,
                    TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            }
            else
            {
                _shellTransform.Y = 0;
            }
        }
        else
        {
            _shellTransform.BeginAnimation(TranslateTransform.YProperty, null);
            _shellTransform.Y = 0;
        }

        RestartHideTimer();
    }

    private void EnsureWindow()
    {
        if (_window is not null)
            return;

        _iconPath = new Path
        {
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            StrokeThickness = 1.65,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Data = Geometry.Parse(VolumeGeometry)
        };
        _iconPath.SetResourceReference(Shape.StrokeProperty, "Tc.Text");

        _iconButton = new Button
        {
            Width = 34,
            Height = 34,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Content = _iconPath,
            Cursor = System.Windows.Input.Cursors.Hand,
            Style = WpfApplication.Current?.TryFindResource("TcIconButton") as Style
        };
        _iconButton.SetResourceReference(Control.BackgroundProperty, "Tc.SurfaceAlt");
        _iconButton.Click += (_, _) =>
        {
            if (_activeLabel.Contains("Volume", StringComparison.OrdinalIgnoreCase) || _activeLabel.Contains("Muted", StringComparison.OrdinalIgnoreCase))
                _toggleMute();
            RestartHideTimer();
        };

        _label = new TextBlock
        {
            FontFamily = new MediaFontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _label.SetResourceReference(TextBlock.ForegroundProperty, "Tc.Text");

        _value = new TextBlock
        {
            FontFamily = new MediaFontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 10.5,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        _value.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");

        var header = new Grid { Margin = new Thickness(0, 0, 0, 1) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(_label);
        Grid.SetColumn(_value, 1);
        header.Children.Add(_value);

        _slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Height = 22,
            IsMoveToPointEnabled = true,
            SmallChange = 1,
            LargeChange = 5,
            Style = WpfApplication.Current?.TryFindResource("TcSlider") as Style
        };
        _slider.ValueChanged += (_, e) =>
        {
            if (_syncing || !_slider.IsMouseCaptureWithin)
                return;
            int current = (int)Math.Round(e.NewValue);
            if (_setValue(_activeLabel, current))
                _value!.Text = $"{current}%";
            RestartHideTimer();
        };
        _slider.PreviewMouseDown += (_, _) => _hideTimer.Stop();
        _slider.PreviewMouseUp += (_, _) => RestartHideTimer();

        var right = new Grid { Margin = new Thickness(9, 0, 0, 0) };
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.Children.Add(header);
        Grid.SetRow(_slider, 1);
        right.Children.Add(_slider);

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        content.ColumnDefinitions.Add(new ColumnDefinition());
        content.Children.Add(_iconButton);
        Grid.SetColumn(right, 1);
        content.Children.Add(right);

        _shellTransform = new TranslateTransform();
        _shell = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 7, 12, 7),
            RenderTransform = _shellTransform,
            Child = content
        };
        _shell.SetResourceReference(Border.BackgroundProperty, "Tc.Surface");
        _shell.SetResourceReference(Border.BorderBrushProperty, "Tc.BorderStrong");
        _shell.MouseEnter += (_, _) => _hideTimer.Stop();
        _shell.MouseLeave += (_, _) => RestartHideTimer();

        var clippingHost = new Grid
        {
            ClipToBounds = true,
            Background = Brushes.Transparent
        };
        clippingHost.Children.Add(_shell);

        _window = new Window
        {
            Width = 286,
            Height = 68,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Content = clippingHost
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
        if (_window is null || !_window.IsVisible || _shellTransform is null)
            return;

        if (!SystemParameters.ClientAreaAnimation)
        {
            _window.Hide();
            _shellTransform.Y = 0;
            return;
        }

        _shellTransform.BeginAnimation(TranslateTransform.YProperty, null);
        var animation = new DoubleAnimation(
            _shellTransform.Y,
            _window.Height + 10,
            TimeSpan.FromMilliseconds(210))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        animation.Completed += (_, _) =>
        {
            if (_window is null || _shellTransform is null)
                return;
            _window.Hide();
            _shellTransform.BeginAnimation(TranslateTransform.YProperty, null);
            _shellTransform.Y = 0;
        };
        _shellTransform.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    public void Dispose()
    {
        _hideTimer.Stop();
        try { _window?.Close(); } catch { }
        _window = null;
    }

    private const string VolumeGeometry = "M2,7 L5,7 L9,4 L9,14 L5,11 L2,11 Z M12,7 C13.5,8 13.5,10 12,11 M14,5 C17,7 17,11 14,13";
    private const string BrightnessGeometry = "M9,2 L9,4 M9,14 L9,16 M2,9 L4,9 M14,9 L16,9 M4,4 L5.5,5.5 M12.5,12.5 L14,14 M14,4 L12.5,5.5 M5.5,12.5 L4,14 M9,6 A3,3 0 1 0 9,12 A3,3 0 1 0 9,6";
}
