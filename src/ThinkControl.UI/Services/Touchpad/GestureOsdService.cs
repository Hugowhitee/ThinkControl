using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const double HorizontalScreenInset = 22;
    private const double TaskbarInset = 8;
    private const double RestingOffset = 0;
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
            next ? ResolveResourceGeometry(SemanticIconKeys.Next) : ResolveResourceGeometry(SemanticIconKeys.Previous));
    }

    internal void ShowTrackCenter() => ShowMediaCommand("Play / pause", ResolveResourceGeometry(SemanticIconKeys.PlayPause));

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

        // Keep the host itself above the taskbar and animate the card inside that
        // host. Alpha.28 settled the shell at a negative Y offset, which meant the
        // clipping host could cut several pixels from the top edge of the OSD.
        _window.Top = area.Bottom - _window.Height - TaskbarInset;

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
            Data = ResolveResourceGeometry(SemanticIconKeys.Volume),
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
            FontSize = TypographyScale.Caption,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _label.SetResourceReference(TextBlock.ForegroundProperty, "Tc.Text");

        _value = new TextBlock
        {
            FontFamily = new MediaFontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = TypographyScale.Caption,
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
            Focusable = false,
            Topmost = true,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Content = clippingHost
        };
        _window.SourceInitialized += (_, _) => ApplyNoActivateStyle(_window);
    }

    private static void ApplyNoActivateStyle(Window window)
    {
        try
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            IntPtr current = GetWindowLongPtr(hwnd, GwlExStyle);
            long next = current.ToInt64() | WsExNoActivate | WsExToolWindow;
            _ = SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(next));
        }
        catch
        {
            // ShowActivated=false remains the managed fallback.
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

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
            ApplyFilledIcon(SemanticIconKeys.Brightness);
            return;
        }

        string key = label.Contains("Muted", StringComparison.OrdinalIgnoreCase) || value == 0
            ? SemanticIconKeys.VolumeMuted
            : value < 45 ? "Tc.Icon.AudioLow" : SemanticIconKeys.Volume;
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
