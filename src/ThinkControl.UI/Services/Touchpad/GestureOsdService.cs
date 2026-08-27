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
    private static readonly Geometry BrightnessSunGeometry = Geometry.Parse(
        "M12,2 L12,4 M12,20 L12,22 M4.93,4.93 L6.34,6.34 M17.66,17.66 L19.07,19.07 " +
        "M2,12 L4,12 M20,12 L22,12 M4.93,19.07 L6.34,17.66 M17.66,6.34 L19.07,4.93 " +
        "M12,7 A5,5 0 1 0 12,17 A5,5 0 1 0 12,7");
    private static readonly Geometry PlayPauseGeometry = Geometry.Parse(
        "M4,3 L13,12 L4,21 Z M15,4 L18,4 L18,20 L15,20 Z M20,4 L23,4 L23,20 L20,20 Z");
    private const double HorizontalScreenInset = 22;
    private const double TaskbarRevealOverlap = 1;
    private const double RestingOffset = -8;
    private const double HiddenOffset = 76;

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
    private int _revealGeneration;

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
            Interval = TimeSpan.FromMilliseconds(1080)
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
            _slider.Visibility = Visibility.Visible;
            _slider.Value = clamped;
            _slider.IsEnabled = volume || brightness;
            _iconButton.IsEnabled = true;
            _iconButton.IsHitTestVisible = volume;
            _iconButton.Cursor = volume ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow;
            _iconButton.ToolTip = volume ? "Mute / unmute" : brightness ? "Brightness" : null;
            ApplyIcon(brightness, clamped, label);
        }
        finally
        {
            _syncing = false;
        }

        ApplyBackdrop(settings);
        PositionAndReveal(settings);
        RestartHideTimer();
    }

    internal void ShowTrack(bool next)
    {
        ShowMediaCommand(
            next ? "Next track" : "Previous track",
            next ? ResolveResourceGeometry("Tc.Icon.SkipNext") : ResolveResourceGeometry("Tc.Icon.SkipPrevious"));
    }

    internal void ShowTrackCenter() => ShowMediaCommand("Play / pause", PlayPauseGeometry);

    private void ShowMediaCommand(string label, Geometry geometry)
    {
        ThinkControlUserSettings settings = _settings();
        if (!settings.TouchpadOsdEnabled)
            return;

        EnsureWindow();
        if (_window is null || _label is null || _value is null || _slider is null || _shell is null ||
            _shellTransform is null || _iconPath is null || _iconButton is null)
            return;

        _activeLabel = label;
        _label.Text = label;
        _value.Text = string.Empty;
        _slider.Visibility = Visibility.Collapsed;
        _iconButton.IsEnabled = true;
        _iconButton.IsHitTestVisible = false;
        _iconButton.Cursor = System.Windows.Input.Cursors.Arrow;
        _iconButton.ToolTip = label;
        _iconPath.Data = geometry;
        _iconPath.Stroke = null;
        _iconPath.StrokeThickness = 0;
        _iconPath.SetResourceReference(Shape.FillProperty, "Tc.Text");

        ApplyBackdrop(settings);
        PositionAndReveal(settings);
        RestartHideTimer();
    }

    internal Window PrepareForSnapshot(string label, int value, bool? nextTrack = null)
    {
        if (nextTrack is bool next)
            ShowTrack(next);
        else
            Show(label, value);
        _hideTimer.Stop();
        if (_shellTransform is not null)
        {
            _shellTransform.BeginAnimation(TranslateTransform.YProperty, null);
            _shellTransform.Y = RestingOffset;
        }
        return _window ?? throw new InvalidOperationException("Gesture OSD window was not created.");
    }

    private void ApplyBackdrop(ThinkControlUserSettings settings)
    {
        if (_window is null || _shell is null)
            return;
        Brush backdrop = WpfApplication.Current?.TryFindResource("Tc.Surface") as Brush ?? Brushes.Black;
        Brush translucentBackdrop = backdrop.CloneCurrentValue();
        translucentBackdrop.Opacity = Math.Clamp(settings.TouchpadOsdOpacity, 0, 1.0);
        _shell.Background = translucentBackdrop;
        _shell.Opacity = 1;
        _window.Opacity = 1;
    }

    private void PositionAndReveal(ThinkControlUserSettings settings)
    {
        if (_window is null || _shellTransform is null)
            return;

        Rect area = SystemParameters.WorkArea;
        _window.Left = settings.TouchpadOsdPosition switch
        {
            "Left" => area.Left + HorizontalScreenInset,
            "Right" => area.Right - _window.Width - HorizontalScreenInset,
            _ => area.Left + (area.Width - _window.Width) / 2d
        };

        // Keep the transparent clipping host flush with the taskbar boundary so the
        // card still emerges from behind it. The visible shell then settles a few
        // pixels above the boundary; this preserves the Windows-like reveal without
        // leaving the card visually glued to the taskbar.
        _window.Top = area.Bottom - _window.Height + TaskbarRevealOverlap;

        bool alreadyVisible = _window.IsVisible;
        int generation = ++_revealGeneration;
        _shellTransform.BeginAnimation(TranslateTransform.YProperty, null);
        if (!alreadyVisible)
        {
            _shellTransform.Y = HiddenOffset;
            _window.Show();
            if (SystemParameters.ClientAreaAnimation)
            {
                var reveal = new DoubleAnimation(HiddenOffset, RestingOffset, TimeSpan.FromMilliseconds(122))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                reveal.Completed += (_, _) =>
                {
                    if (_shellTransform is null || generation != _revealGeneration)
                        return;
                    _shellTransform.BeginAnimation(TranslateTransform.YProperty, null);
                    _shellTransform.Y = RestingOffset;
                };
                _shellTransform.BeginAnimation(TranslateTransform.YProperty, reveal);
            }
            else
            {
                _shellTransform.Y = RestingOffset;
            }
        }
        else
        {
            _shellTransform.Y = RestingOffset;
        }
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
            Data = ResolveResourceGeometry("Tc.Icon.Audio"),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        _iconPath.SetResourceReference(Shape.FillProperty, "Tc.Text");

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
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _label.SetResourceReference(TextBlock.ForegroundProperty, "Tc.Text");

        _value = new TextBlock
        {
            FontFamily = new MediaFontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 12,
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
            Height = 30,
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

        int generation = ++_revealGeneration;
        if (!SystemParameters.ClientAreaAnimation)
        {
            _window.Hide();
            _shellTransform.Y = RestingOffset;
            return;
        }

        _shellTransform.BeginAnimation(TranslateTransform.YProperty, null);
        var animation = new DoubleAnimation(
            _shellTransform.Y,
            HiddenOffset,
            TimeSpan.FromMilliseconds(148))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        animation.Completed += (_, _) =>
        {
            if (_window is null || _shellTransform is null || generation != _revealGeneration)
                return;
            _window.Hide();
            _shellTransform.BeginAnimation(TranslateTransform.YProperty, null);
            _shellTransform.Y = RestingOffset;
        };
        _shellTransform.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    public void Dispose()
    {
        _hideTimer.Stop();
        try { _window?.Close(); } catch { }
        _window = null;
    }

    private void ApplyIcon(bool brightness, int value, string label)
    {
        if (_iconPath is null)
            return;

        if (brightness)
        {
            _iconPath.Data = BrightnessSunGeometry;
            _iconPath.Fill = Brushes.Transparent;
            _iconPath.SetResourceReference(Shape.StrokeProperty, "Tc.Text");
            _iconPath.StrokeThickness = 1.75;
            return;
        }

        string key = label.Contains("Muted", StringComparison.OrdinalIgnoreCase) || value == 0
            ? "Tc.Icon.AudioMuted"
            : value < 45 ? "Tc.Icon.AudioLow" : "Tc.Icon.Audio";
        ApplyFilledIcon(key);
    }

    private void ApplyFilledIcon(string key)
    {
        if (_iconPath is null)
            return;
        _iconPath.Data = ResolveResourceGeometry(key);
        _iconPath.Stroke = null;
        _iconPath.StrokeThickness = 0;
        _iconPath.SetResourceReference(Shape.FillProperty, "Tc.Text");
    }

    private static Geometry ResolveResourceGeometry(string key) =>
        WpfApplication.Current?.TryFindResource(key) as Geometry ?? Geometry.Empty;
}
