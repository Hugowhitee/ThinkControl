using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ThinkControl.UI.Services;
using ThinkControl.Core.Notifications;

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

    internal void ToggleNotificationSheet()
    {
        if (_notificationOverlay?.Visibility == Visibility.Visible)
            HideNotificationSheet();
        else
            ShowNotificationSheet();
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
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(24, 0, 0, 0)),
            Cursor = System.Windows.Input.Cursors.Arrow
        };
        backdrop.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            HideNotificationSheet();
        };

        var title = new TextBlock
        {
            Text = "Inbox",
            FontSize = TypographyScale.Value,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        var close = new Button
        {
            Content = "×",
            Width = 30,
            Height = 30,
            FontSize = TypographyScale.Value,
            Padding = new Thickness(0),
            ToolTip = "Close Inbox",
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
            FontSize = TypographyScale.Caption,
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
                setup = new HardwareSetupStatus(false, false, true, false, false, false,
                    "Could not query ThinkControl hardware service", "Provider status unavailable");
            }

            string hardwareDetail = _app.State.HardwareAccess ?? string.Empty;
            bool pawnIoRepair = setup.LowLevelAccessRelevant &&
                ((setup.LowLevelAccessRegistered && !setup.LowLevelAccessInstalled) || IsPawnIoRepairFailure(hardwareDetail));
            bool verifiedX9 = IsVerifiedX9(_app.State.MachineType);
            bool ecCompatibilityFailure = verifiedX9 && IsEcCompatibilityFailure(hardwareDetail);

            var messages = new List<SheetMessage>();

            UpdateCheckResult? update = _app.LatestUpdateResult;
            if (update is { Available: true })
            {
                string transition = UpdatePromptPolicy.Transition(UpdateService.CurrentVersion, update.Version);
                bool promptDismissed = UpdatePromptPolicy.IsDismissed(
                    update.Version,
                    _app.UserSettings.Current.DismissedUpdateVersion);
                string detail = $"{transition}. " +
                    (promptDismissed
                        ? "The startup prompt was dismissed, but the update remains available here until you install it or a newer release replaces it."
                        : "A newer release is ready. Open Updates to review and install it.");
                messages.Add(new(
                    "ThinkControl update available",
                    detail,
                    "Open Updates",
                    SheetAction.Updates,
                    true));
            }

            if (!setup.ServiceRunning || !setup.ServiceReachable)
            {
                messages.Add(new(
                    "Hardware service",
                    setup.ServiceDetail + ". ThinkControl can repair and start its own service after one Windows approval.",
                    "Review repair",
                    SheetAction.Service,
                    true));
            }

            if (setup.LowLevelAccessRelevant && !setup.LowLevelAccessInstalled)
            {
                bool registered = setup.LowLevelAccessRegistered;
                messages.Add(new(
                    registered ? "PawnIO needs repair" : "PawnIO installation required",
                    registered
                        ? $"ThinkControl found PawnIO registration, but the driver installation is incomplete: {setup.LowLevelAccessDetail}. Repair restores the pinned verified component before dependent providers are retried."
                        : verifiedX9
                            ? "PawnIO is required for X9 sensor discovery and the verified EC provider. ThinkControl downloads the pinned package, verifies SHA-256, then asks Windows once before installation."
                            : "An additional low-level provider is required for the detected hardware. ThinkControl verifies the pinned package before Windows is asked to install it.",
                    registered ? "Review repair" : "Install PawnIO",
                    SheetAction.PawnIo,
                    true));
            }
            else if (pawnIoRepair)
            {
                messages.Add(new(
                    "PawnIO needs repair",
                    FriendlyPawnIoDetail(hardwareDetail),
                    "Review repair",
                    SheetAction.PawnIo,
                    true));
            }

            if (!_app.State.CanSensorTelemetry && !pawnIoRepair && setup.ServiceRunning && setup.ServiceReachable &&
                setup.LowLevelAccessRelevant && setup.LowLevelAccessInstalled)
            {
                messages.Add(new(
                    "Sensors",
                    "The sensor provider has not produced usable telemetry. Retry rebuilds only the sensor stack; fan EC and keyboard providers stay untouched.",
                    "Review retry",
                    SheetAction.Sensors,
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
                    ecCompatibilityFailure ? string.Empty : "Review retry",
                    ecCompatibilityFailure ? SheetAction.None : SheetAction.FanControl,
                    true));
            }

            if (!_app.State.CanKeyboardBacklight && !pawnIoRepair && verifiedX9 && setup.ServiceRunning && setup.ServiceReachable)
            {
                messages.Add(new(
                    "Keyboard",
                    "The Lenovo keyboard provider has not produced a valid readback. Retry probes only keyboard backlight contracts and does not recycle working sensors or fan control.",
                    "Review retry",
                    SheetAction.Keyboard,
                    true));
            }

            if (DeviceSupportReportService.HasUsefulDiscovery(_app.State))
            {
                messages.Add(new(
                    "Device compatibility data is ready",
                    DeviceSupportReportService.DiscoverySummary(_app.State) + ". With compatibility sharing enabled, the redacted hardware-only report is prepared locally and GitHub submission remains explicit.",
                    "Share device report",
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
            _notificationSummary.Text = attention switch
            {
                0 => "You're all caught up",
                1 => "1 item needs attention",
                _ => $"{attention} items need attention"
            };

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
            FontSize = TypographyScale.Caption,
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

    private void NotificationAction_Click(object sender, RoutedEventArgs e)
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
                case SheetAction.Service:
                case SheetAction.PawnIo:
                case SheetAction.Sensors:
                case SheetAction.FanControl:
                case SheetAction.Keyboard:
                    HideNotificationSheet();
                    _app.OpenHardwareIssue(action switch
                    {
                        SheetAction.Service => HardwarePrerequisiteIssue.Service,
                        SheetAction.PawnIo => HardwarePrerequisiteIssue.PawnIo,
                        SheetAction.Sensors => HardwarePrerequisiteIssue.Sensors,
                        SheetAction.FanControl => HardwarePrerequisiteIssue.FanControl,
                        _ => HardwarePrerequisiteIssue.Keyboard
                    });
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
        Service,
        PawnIo,
        Sensors,
        FanControl,
        Keyboard,
        Diagnostics
    }
}
