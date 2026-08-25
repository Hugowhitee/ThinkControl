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
        _notificationSummary.Text = "Checking app and hardware status…";
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

            string hardwareDetail = _app.State.HardwareAccess ?? string.Empty;
            bool pawnIoRepair = setup.LowLevelAccessRelevant && IsPawnIoRepairFailure(hardwareDetail);
            bool verifiedX9 = IsVerifiedX9(_app.State.MachineType);
            bool ecCompatibilityFailure = verifiedX9 && IsEcCompatibilityFailure(hardwareDetail);

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

            if (!setup.ServiceRunning || !setup.ServiceReachable)
            {
                messages.Add(new(
                    "Hardware service",
                    setup.ServiceDetail + ". ThinkControl can repair and start its own service after one Windows approval.",
                    "Fix required components",
                    SheetAction.HardwareRepair,
                    true));
            }

            if (setup.LowLevelAccessRelevant && !setup.LowLevelAccessInstalled)
            {
                messages.Add(new(
                    "Low-level hardware access",
                    verifiedX9
                        ? "PawnIO is required for X9 sensor discovery and the verified EC provider. ThinkControl downloads the pinned package, verifies SHA-256, then asks Windows once before installation."
                        : "An additional low-level provider is required for the detected hardware. ThinkControl verifies the pinned package before Windows is asked to install it.",
                    "Fix required components",
                    SheetAction.HardwareRepair,
                    true));
            }
            else if (pawnIoRepair)
            {
                messages.Add(new(
                    "Low-level hardware access",
                    FriendlyPawnIoDetail(hardwareDetail),
                    "Repair component",
                    SheetAction.HardwareRepair,
                    true));
            }

            if (!_app.State.CanSensorTelemetry && !pawnIoRepair && setup.ServiceRunning && setup.ServiceReachable &&
                setup.LowLevelAccessRelevant && setup.LowLevelAccessInstalled)
            {
                messages.Add(new(
                    "Sensors",
                    "The configured sensor provider has not produced usable telemetry. Retry performs one clean provider rebuild; it does not reinstall a working driver.",
                    "Retry sensors",
                    SheetAction.RefreshProviders,
                    true));
            }

            if (verifiedX9 && !_app.State.CanFanControl && !pawnIoRepair && setup.ServiceRunning && setup.ServiceReachable)
            {
                string detail = ecCompatibilityFailure
                    ? "Low-level access is working, but neither supported ThinkPad EC port pair passed read-only X9 validation. Lenovo firmware remains in control; no fan write is attempted."
                    : _app.State.CanFanTelemetry
                        ? "Fan telemetry is visible, but the verified X9 EC control/readback gate has not passed. Lenovo firmware remains in control."
                        : "The verified X9 fan provider has not produced telemetry yet. Lenovo firmware remains in control.";

                messages.Add(new(
                    "Fans",
                    detail,
                    ecCompatibilityFailure ? string.Empty : "Retry provider",
                    ecCompatibilityFailure ? SheetAction.None : SheetAction.RefreshProviders,
                    true));
            }

            // PawnIO/EC readiness is the root cause for multiple dependent X9
            // capabilities. While that component needs repair, do not repeat the same
            // underlying problem as a separate Keyboard card; provider-specific cards
            // return after low-level access itself is healthy.
            if (!_app.State.CanKeyboardBacklight && !pawnIoRepair && verifiedX9 && setup.ServiceRunning && setup.ServiceReachable)
            {
                messages.Add(new(
                    "Keyboard",
                    "The Lenovo keyboard provider has not produced a valid readback. Retry probes the installed provider contracts once; failed probes are backed off instead of hammered in the background.",
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
                    "No update or hardware action currently needs your attention. Unsupported capabilities remain visible on their pages without being treated as faults.",
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

    private static bool IsVerifiedX9(string? machineType) =>
        string.Equals(machineType, "21Q6", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(machineType, "21Q7", StringComparison.OrdinalIgnoreCase);

    private static bool IsPawnIoRepairFailure(string detail) =>
        detail.Contains("PawnIO is not installed", StringComparison.OrdinalIgnoreCase) ||
        detail.Contains("too old for", StringComparison.OrdinalIgnoreCase) ||
        detail.Contains("PawnIO is registered but its device is not available", StringComparison.OrdinalIgnoreCase) ||
        detail.Contains("access to its device was denied", StringComparison.OrdinalIgnoreCase) ||
        detail.Contains("LPC/ACPI EC module could not be loaded", StringComparison.OrdinalIgnoreCase) ||
        detail.Contains("PawnIO device could not be opened", StringComparison.OrdinalIgnoreCase);

    private static bool IsEcCompatibilityFailure(string detail) =>
        detail.Contains("neither supported ThinkPad EC port pair", StringComparison.OrdinalIgnoreCase) ||
        detail.Contains("Modern 0x1604/0x1600 and legacy 0x66/0x62 were both rejected", StringComparison.OrdinalIgnoreCase);

    private static string FriendlyPawnIoDetail(string detail)
    {
        if (detail.Contains("too old for", StringComparison.OrdinalIgnoreCase))
            return "The installed low-level provider is older than the verified ThinkControl contract. Repair upgrades the pinned package and then verifies provider readback.";
        if (detail.Contains("access to its device was denied", StringComparison.OrdinalIgnoreCase))
            return "The low-level component is installed, but ThinkControl's hardware service was denied access to its device. Repair it once, then providers can be rechecked.";
        if (detail.Contains("module could not be loaded", StringComparison.OrdinalIgnoreCase))
            return "The low-level device opened, but its sensor/EC module did not load. Repair the pinned component before one clean retry.";
        if (detail.Contains("device is not available", StringComparison.OrdinalIgnoreCase) || detail.Contains("device could not be opened", StringComparison.OrdinalIgnoreCase))
            return "The low-level component is registered in Windows, but its kernel device is not accessible to ThinkControl. Repair it before dependent providers are retried.";
        return "The low-level component did not pass its service-side readiness check. Repair it once and then verify providers again.";
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
                case SheetAction.HardwareRepair:
                    button.Content = "Repairing…";
                    if (_notificationSummary is not null)
                        _notificationSummary.Text = "Repairing required components and verifying readback…";
                    await _app.RepairDetectedHardwareAsync();
                    await RefreshNotificationSheetAsync();
                    break;
                case SheetAction.RefreshProviders:
                    button.Content = "Retrying…";
                    if (_notificationSummary is not null)
                        _notificationSummary.Text = "Refreshing hardware providers and verifying current readback…";
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
        HardwareRepair,
        RefreshProviders,
        Diagnostics
    }
}
