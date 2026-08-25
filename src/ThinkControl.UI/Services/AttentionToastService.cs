using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ThinkControl.UI.Services;

internal sealed class AttentionToastService : IDisposable
{
    private Window? _window;
    private TextBlock? _title;
    private TextBlock? _message;
    private Button? _action;
    private Action? _actionCallback;
    private readonly DispatcherTimer _hideTimer;
    private string _lastKey = string.Empty;
    private DateTimeOffset _lastShown = DateTimeOffset.MinValue;

    internal AttentionToastService()
    {
        _hideTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _hideTimer.Tick += (_, _) => Hide();
    }

    internal void Show(string key, string title, string message, string actionText, Action action)
    {
        // Repeated status refreshes must not keep re-opening the same notice.
        if (key == _lastKey && DateTimeOffset.UtcNow - _lastShown < TimeSpan.FromMinutes(10))
            return;

        EnsureWindow();
        if (_window is null || _title is null || _message is null || _action is null)
            return;

        _lastKey = key;
        _lastShown = DateTimeOffset.UtcNow;
        _title.Text = title;
        _message.Text = message;
        _action.Content = actionText;
        _actionCallback = action;

        Rect area = SystemParameters.WorkArea;
        double left = area.Right - _window.Width - 18;
        double top = area.Bottom - _window.Height - 18;
        _window.Left = left;
        _window.Top = top;

        if (!_window.IsVisible)
        {
            _window.Opacity = 0;
            _window.Show();
            if (SystemParameters.ClientAreaAnimation)
            {
                _window.BeginAnimation(Window.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130)));
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

    private void EnsureWindow()
    {
        if (_window is not null)
            return;

        _title = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        _title.SetResourceReference(TextBlock.ForegroundProperty, "Tc.Text");

        _message = new TextBlock
        {
            FontSize = 10.5,
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 42
        };
        _message.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");

        _action = new Button
        {
            Height = 30,
            MinWidth = 82,
            Padding = new Thickness(12, 3, 12, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = Application.Current.TryFindResource("TcButton") as Style
        };
        _action.Click += (_, _) =>
        {
            Hide();
            _actionCallback?.Invoke();
        };

        var dismiss = new Button
        {
            Content = "Later",
            Height = 30,
            Padding = new Thickness(10, 3, 10, 3),
            Margin = new Thickness(7, 0, 0, 0),
            Style = Application.Current.TryFindResource("TcButton") as Style
        };
        dismiss.Click += (_, _) => Hide();

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0)
        };
        actions.Children.Add(_action);
        actions.Children.Add(dismiss);

        var content = new StackPanel();
        content.Children.Add(_title);
        content.Children.Add(_message);
        content.Children.Add(actions);

        var shell = new Border
        {
            Padding = new Thickness(14, 12, 14, 12),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Child = content
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
            Width = 350,
            Height = 126,
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
