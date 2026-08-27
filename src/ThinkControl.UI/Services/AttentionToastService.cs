using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI.Services;

internal sealed class AttentionToastService : IDisposable
{
    private Window? _window;
    private TextBlock? _title;
    private TextBlock? _message;
    private Button? _action;
    private Button? _dismiss;
    private StackPanel? _actions;
    private Action? _actionCallback;
    private Action? _dismissCallback;
    private readonly DispatcherTimer _hideTimer;
    private string _lastKey = string.Empty;
    private DateTimeOffset _lastShown = DateTimeOffset.MinValue;

    internal Window? WindowForShellSmoke => _window;
    internal Button? ActionButtonForShellSmoke => _action;

    internal AttentionToastService()
    {
        _hideTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _hideTimer.Tick += (_, _) => Hide();
    }

    internal void Show(string key, string title, string message, string actionText, Action action, Action? dismissed = null)
    {
        if (!Prepare(key, title, message))
            return;

        _action!.Content = actionText;
        _action.Visibility = Visibility.Visible;
        _dismiss!.Visibility = Visibility.Visible;
        _actions!.Visibility = Visibility.Visible;
        _actionCallback = action;
        _dismissCallback = dismissed;
        _hideTimer.Interval = TimeSpan.FromSeconds(10);
        Present();
    }

    internal void ShowPassive(string key, string title, string message, TimeSpan? duration = null)
    {
        if (!Prepare(key, title, message))
            return;

        _actionCallback = null;
        _dismissCallback = null;
        _action!.Visibility = Visibility.Collapsed;
        _dismiss!.Visibility = Visibility.Collapsed;
        _actions!.Visibility = Visibility.Collapsed;
        _hideTimer.Interval = duration ?? TimeSpan.FromSeconds(4.5);
        Present();
    }

    private bool Prepare(string key, string title, string message)
    {
        if (key == _lastKey && DateTimeOffset.UtcNow - _lastShown < TimeSpan.FromMinutes(10))
            return false;

        EnsureWindow();
        if (_window is null || _title is null || _message is null || _action is null || _dismiss is null || _actions is null)
            return false;

        _lastKey = key;
        _lastShown = DateTimeOffset.UtcNow;
        _title.Text = title;
        _message.Text = message;
        return true;
    }

    private void Present()
    {
        if (_window is null)
            return;

        Window? anchor = ResolveOwner();
        if (anchor is not null && !ReferenceEquals(_window.Owner, anchor))
        {
            try { _window.Owner = anchor; }
            catch
            {
                // Ownership is lifecycle polish, not a reason to lose a notification.
                // App-level focus classification still treats our HWND as internal.
            }
        }

        _window.UpdateLayout();
        Rect area = SystemParameters.WorkArea;
        double width = Math.Max(_window.ActualWidth, _window.Width);
        double height = Math.Max(_window.ActualHeight, _window.MinHeight);
        PositionWindow(anchor, area, width, height);

        if (!_window.IsVisible)
        {
            _window.Opacity = 0;
            _window.Show();
            _window.UpdateLayout();
            height = Math.Max(_window.ActualHeight, _window.MinHeight);
            PositionWindow(anchor, area, width, height);
            if (SystemParameters.ClientAreaAnimation)
            {
                _window.BeginAnimation(Window.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            }
            else
            {
                _window.Opacity = 1;
            }
        }
        else
        {
            _window.BeginAnimation(Window.OpacityProperty, null);
            _window.Opacity = 1;
        }

        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private Window? ResolveOwner()
    {
        if (System.Windows.Application.Current is not Application app)
            return null;

        Window[] windows = app.Windows.OfType<Window>()
            .Where(window => window.IsVisible && !ReferenceEquals(window, _window))
            .ToArray();

        return windows.FirstOrDefault(window => window.IsActive)
            ?? windows.OfType<MainWindow>().FirstOrDefault()
            ?? windows.OfType<AdvancedWindow>().FirstOrDefault()
            ?? windows.FirstOrDefault(window => window.ShowInTaskbar);
    }

    private void PositionWindow(Window? anchor, Rect area, double width, double height)
    {
        if (_window is null)
            return;

        if (anchor is MainWindow compact && compact.IsVisible)
        {
            // Do not cover the controls that caused the notification to become a
            // destructive focus interaction in previous releases. Keep the toast
            // aligned with Compact, but place it above the flyout when space allows.
            _window.Left = Math.Clamp(compact.Left, area.Left + 8, Math.Max(area.Left + 8, area.Right - width - 8));
            double above = compact.Top - height - 10;
            _window.Top = above >= area.Top + 8
                ? above
                : Math.Clamp(compact.Top + compact.ActualHeight + 10, area.Top + 8, Math.Max(area.Top + 8, area.Bottom - height - 8));
            return;
        }

        _window.Left = area.Right - width - 18;
        _window.Top = area.Bottom - height - 18;
    }

    private void EnsureWindow()
    {
        if (_window is not null)
            return;

        var brand = new BrandMark
        {
            Width = 24,
            Height = 24,
            Margin = new Thickness(0, 0, 9, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var appName = new TextBlock
        {
            Text = "ThinkControl",
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        appName.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");

        var brandHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 9)
        };
        brandHeader.Children.Add(brand);
        brandHeader.Children.Add(appName);

        _title = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        _title.SetResourceReference(TextBlock.ForegroundProperty, "Tc.Text");

        _message = new TextBlock
        {
            FontSize = 10.5,
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 68
        };
        _message.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");

        _action = new Button
        {
            MinHeight = 32,
            MinWidth = 104,
            Padding = new Thickness(13, 4, 13, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = System.Windows.Application.Current?.TryFindResource("TcButton") as Style
        };
        _action.Click += (_, _) => InvokeAndRestoreOwner(_actionCallback);

        _dismiss = new Button
        {
            Content = "Later",
            MinHeight = 32,
            MinWidth = 78,
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(8, 0, 0, 0),
            Style = System.Windows.Application.Current?.TryFindResource("TcButton") as Style
        };
        _dismiss.Click += (_, _) => InvokeAndRestoreOwner(_dismissCallback);

        _actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 13, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _actions.Children.Add(_action);
        _actions.Children.Add(_dismiss);

        var content = new StackPanel();
        content.Children.Add(brandHeader);
        content.Children.Add(_title);
        content.Children.Add(_message);
        content.Children.Add(_actions);

        var shell = new Border
        {
            Padding = new Thickness(16, 13, 16, 14),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = content,
            Effect = new DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 5,
                Opacity = 0.28
            }
        };
        shell.SetResourceReference(Border.BackgroundProperty, "Tc.Surface");
        shell.SetResourceReference(Border.BorderBrushProperty, "Tc.BorderStrong");
        shell.MouseEnter += (_, _) => _hideTimer.Stop();
        shell.MouseLeave += (_, _) =>
        {
            _hideTimer.Stop();
            _hideTimer.Start();
        };

        _window = new Window
        {
            Width = 390,
            MinHeight = 118,
            MaxHeight = 230,
            SizeToContent = SizeToContent.Height,
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

    private void InvokeAndRestoreOwner(Action? callback)
    {
        Window? owner = _window?.Owner;
        Hide();

        // Clicking an owned ThinkControl notification may activate the toast HWND.
        // Restore the underlying app surface before running the command. For an
        // update/navigation action the callback can then deliberately transition it.
        if (owner?.IsVisible == true)
        {
            try { owner.Activate(); }
            catch { }
        }

        callback?.Invoke();
    }

    internal void Hide()
    {
        _hideTimer.Stop();
        if (_window is null || !_window.IsVisible)
            return;

        _window.BeginAnimation(Window.OpacityProperty, null);
        _window.Hide();
    }

    public void Dispose()
    {
        _hideTimer.Stop();
        try { _window?.Close(); } catch { }
        _window = null;
    }
}
