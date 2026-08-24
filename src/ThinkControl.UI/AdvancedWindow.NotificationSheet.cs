using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private Grid? _notificationOverlay;
    private Border? _notificationSheet;
    private StackPanel? _notificationMessages;
    private TextBlock? _notificationSummary;
    private bool _notificationRefreshBusy;

    internal async void ShowNotificationSheet()
    {
        EnsureNotificationSheet();
        if (_notificationOverlay is null || _notificationSheet is null)
            return;

        _notificationOverlay.Visibility = Visibility.Visible;
        Panel.SetZIndex(_notificationOverlay, 200);

        _notificationSheet.BeginAnimation(TranslateTransform.XProperty, null);
        if (_notificationSheet.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            _notificationSheet.RenderTransform = transform;
        }

        if (SystemParameters.ClientAreaAnimation)
        {
            transform.X = 26;
            transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(26, 0, TimeSpan.FromMilliseconds(155))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        }
        else
        {
            transform.X = 0;
        }

        await RefreshNotificationSheetAsync();
    }

    internal void HideNotificationSheet()
    {
        if (_notificationOverlay is null || _notificationOverlay.Visibility != Visibility.Visible)
            return;

        if (!SystemParameters.ClientAreaAnimation || _notificationSheet?.RenderTransform is not TranslateTransform transform)
        {
            _notificationOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        var animation = new DoubleAnimation(transform.X, 24, TimeSpan.FromMilliseconds(105))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        animation.Completed += (_, _) =>
        {
            if (_notificationOverlay is not null)
                _notificationOverlay.Visibility = Visibility.Collapsed;
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = 0;
        };
        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void EnsureNotificationSheet()
    {
        if (_notificationOverlay is not null ||
            Content is not Border { Child: Grid root } ||
            root.Children.OfType<Grid>().FirstOrDefault(grid => Grid.GetRow(grid) == 1) is not Grid body)
        {
            return;
        }

        var backdrop = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(24, 0, 0, 0)),
            Cursor = System.Windows.Input.Cursors.Arrow
        };
        backdrop.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            HideNotificationSheet();
        };

        var title = new TextBlock
        {
            Text = "Notifications",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        var close = new Button
        {
            Content = "×",
            Width = 30,
            Height = 30,
            FontSize = 18,
            Padding = new Thickness(0),
            ToolTip = "Close notifications",
            HorizontalAlignment = HorizontalAlignment.Right,
            Style = TryFindResource("TcIconButton") as Style
        };
        close.Click += (_, _) => HideNotificationSheet();

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(title);
        Grid.SetColumn(close, 1);
        header.Children.Add(close);

        _notificationSummary = new TextBlock
        {
            FontSize = 10.5,
            Margin = new Thickness(0, 5, 0, 12),
            TextWrapping = TextWrapping.Wrap
        };
        _notificationSummary.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");

        _notificationMessages = new StackPanel();
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _notificationMessages
        };

        var refresh = new Button
        {
            Content = "Refresh",
            Style = TryFindResource("TcButton") as Style,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 12, 0, 0),
            Padding = new Thickness(11, 5, 11, 5)
        };
        refresh.Click += async (_, _) => await RefreshNotificationSheetAsync();

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition());
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.Children.Add(header);
        Grid.SetRow(_notificationSummary, 1);
        content.Children.Add(_notificationSummary);
        Grid.SetRow(scroll, 2);
        content.Children.Add(scroll);
        Grid.SetRow(refresh, 3);
        content.Children.Add(refresh);

        _notificationSheet = new Border
        {
            Width = 410,
            MaxWidth = 430,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(14),
            Padding = new Thickness(17, 15, 15, 15),
            CornerRadius = new CornerRadius(11),
            BorderThickness = new Thickness(1),
            RenderTransform = new TranslateTransform(),
            Child = content
        };
        _notificationSheet.SetResourceReference(Border.BackgroundProperty, "Tc.Surface");
        _notificationSheet.SetResourceReference(Border.BorderBrushProperty, "Tc.BorderStrong");
        _notificationSheet.MouseLeftButtonUp += (_, e) => e.Handled = true;

        _notificationOverlay = new Grid
        {
            Visibility = Visibility.Collapsed
        };
        Grid.SetColumn(_notificationOverlay, 1);
        _notificationOverlay.Children.Add(backdrop);
        _notificationOverlay.Children.Add(_notificationSheet);
        body.Children.Add(_notificationOverlay);
    }

    private async Task RefreshNotificationSheetAsync()
    {
        if (_notificationRefreshBusy || _notificationMessages is null || _notificationSummary is null)
            return;

        _notificationRefreshBusy = true;
        _notificationMessages.Children.Clear();
        _notificationSummary.Text = "Refreshing…";
        try
        {
            HardwareSetupStatus setup;
            try
            {
                setup = await _app.RefreshHardwareSetupStatusAsync();
            }
            catch
            {
                setup = new HardwareSetupStatus(false, false, true, false, false,
                    "Could not query ThinkControl hardware service", "Provider status unavailable");
            }

            var messages = new List<SheetMessage>();
            UpdateCheckResult? update = _app.LatestUpdateResult;
            if (update is { Available: true })
            {
                messages.Add(new(
                    "ThinkControl update available",
                    string.IsNullOrWhiteSpace(update.Version)
                        ? update.Status
                        : $"{update.Version} is ready. Open Updates to review and install it.",
                    "Open Updates",
                    SheetAction.Updates,
                    true));
            }

            if (!setup.ServiceRunning)
            {
                messages.Add(new(
                    "Hardware service needs attention",
                    setup.ServiceDetail + ". Repair ThinkControl's service before retrying hardware providers.",
                    "Hardware setup",
                    SheetAction.HardwareSetup,
                    true));
            }

            if (setup.LowLevelAccessRelevant && !setup.LowLevelAccessInstalled)
            {
                messages.Add(new(
                    "PawnIO is not installed",
                    "Low-level sensor and verified X9 EC access are waiting for PawnIO. Hardware setup can install the pinned driver and verify it before retrying providers.",
                    "Install / repair",
                    SheetAction.HardwareSetup,
                    true));
            }

            if (!_app.State.CanSensorTelemetry)
            {
                messages.Add(new(
                    "Sensors are unavailable",
                    setup.LowLevelAccessInstalled
                        ? "PawnIO is present, but the sensor provider has not produced usable telemetry. Retry rebuilds the provider without reinstalling drivers."
                        : "Sensor discovery is waiting for the low-level provider above.",
                    setup.LowLevelAccessInstalled ? "Retry sensors" : "Hardware setup",
                    setup.LowLevelAccessInstalled ? SheetAction.RefreshProviders : SheetAction.HardwareSetup,
                    true));
            }

            if (!_app.State.CanFanControl)
            {
                messages.Add(new(
                    "Fan control is unavailable",
                    _app.State.CanFanTelemetry
                        ? "Fan telemetry is visible, but the X9 EC read/write validation has not passed. Lenovo firmware remains in control."
                        : "Fan telemetry and the verified EC control path are not ready yet. Lenovo firmware remains in control.",
                    "Retry provider",
                    SheetAction.RefreshProviders,
                    true));
            }

            if (!_app.State.CanKeyboardBacklight)
            {
                messages.Add(new(
                    "Keyboard control is unavailable",
                    "The Lenovo keyboard provider has not produced a valid readback. Retry probes the installed Lenovo PM/Energy driver contracts without installing anything.",
                    "Retry keyboard",
                    SheetAction.RefreshProviders,
                    true));
            }

            if (DeviceSupportReportService.HasUsefulDiscovery(_app.State))
            {
                messages.Add(new(
                    "Useful device support data is ready",
                    DeviceSupportReportService.DiscoverySummary(_app.State) + ". Review the hardware-only report before sharing it.",
                    "Review sharing",
                    SheetAction.Diagnostics,
                    false));
            }

            if (messages.Count == 0)
            {
                messages.Add(new(
                    "Everything looks ready",
                    "No update or hardware action currently needs your attention.",
                    string.Empty,
                    SheetAction.None,
                    false));
            }

            int attention = messages.Count(message => message.Attention);
            _notificationSummary.Text = attention > 0
                ? $"{attention} item{(attention == 1 ? string.Empty : "s")} need attention"
                : "You're all caught up";

            foreach (SheetMessage message in messages)
                _notificationMessages.Children.Add(CreateNotificationCard(message));
        }
        finally
        {
            _notificationRefreshBusy = false;
        }
    }

    private FrameworkElement CreateNotificationCard(SheetMessage message)
    {
        var dot = new Ellipse
        {
            Width = 7,
            Height = 7,
            Margin = new Thickness(0, 5, 10, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        dot.SetResourceReference(Shape.FillProperty, message.Attention ? "Tc.Accent" : "Tc.Success");

        var copy = new StackPanel();
        copy.Children.Add(new TextBlock { Text = message.Title, FontWeight = FontWeights.SemiBold });
        var detail = new TextBlock
        {
            Text = message.Detail,
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(detail);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(dot);
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);

        if (!string.IsNullOrWhiteSpace(message.Button))
        {
            var button = new Button
            {
                Content = message.Button,
                Style = TryFindResource("TcButton") as Style,
                Padding = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(17, 10, 0, 0),
                Tag = message.Action
            };
            button.Click += NotificationAction_Click;
            var wrapper = new StackPanel();
            wrapper.Children.Add(grid);
            wrapper.Children.Add(button);
            return new Border
            {
                Style = TryFindResource("TcSection") as Style,
                Margin = new Thickness(0, 0, 0, 9),
                Child = wrapper
            };
        }

        return new Border
        {
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 0, 0, 9),
            Child = grid
        };
    }

    private async void NotificationAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SheetAction action } button)
            return;

        button.IsEnabled = false;
        try
        {
            switch (action)
            {
                case SheetAction.Updates:
                    HideNotificationSheet();
                    Navigate("Updates");
                    break;
                case SheetAction.HardwareSetup:
                    HideNotificationSheet();
                    Navigate("System");
                    _app.OpenHardwareSetup();
                    break;
                case SheetAction.RefreshProviders:
                    button.Content = "Retrying…";
                    await _app.RefreshHardwareProvidersAsync();
                    await RefreshNotificationSheetAsync();
                    break;
                case SheetAction.Diagnostics:
                    HideNotificationSheet();
                    Navigate("Settings");
                    break;
            }
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private sealed record SheetMessage(string Title, string Detail, string Button, SheetAction Action, bool Attention);

    private enum SheetAction
    {
        None,
        Updates,
        HardwareSetup,
        RefreshProviders,
        Diagnostics
    }
}