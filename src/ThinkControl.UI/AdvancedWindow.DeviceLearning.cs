using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string DeviceLearningStatusResourceKey = "ThinkControl.DeviceLearningStatus";
    private Button? _deviceLearningStatusButton;
    private TextBlock? _deviceLearningBaseLabel;
    private bool _deviceLearningStatusSubscribed;

    private void ConfigureDeviceLearningIndicator()
    {
        if (_deviceLearningStatusButton is null)
        {
            Button? notificationSlot = FindVisualChildren<Button>(this)
                .FirstOrDefault(button => Equals(button.Tag, "ThinkControl.NotificationSlot"));
            if (notificationSlot?.Parent is not Grid dockRow)
                return;

            _deviceLearningBaseLabel = dockRow.Children
                .OfType<TextBlock>()
                .FirstOrDefault(text => string.Equals(text.Text, "Advanced", StringComparison.Ordinal));

            _deviceLearningStatusButton = new Button
            {
                Tag = DeviceLearningStatusResourceKey,
                Style = TryFindResource("TcInlineButton") as Style,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 3, 4, 3),
                Margin = new Thickness(0),
                FontSize = 9.5,
                MaxWidth = 108,
                Visibility = Visibility.Collapsed,
                ToolTip = "Compatibility learning runs quietly in the background while you use ThinkControl. Nothing is uploaded automatically."
            };
            _deviceLearningStatusButton.Click += (_, _) => Navigate("Settings");
            Grid.SetColumn(_deviceLearningStatusButton, 0);
            Panel.SetZIndex(_deviceLearningStatusButton, 2);
            dockRow.Children.Add(_deviceLearningStatusButton);
            Resources[DeviceLearningStatusResourceKey] = _deviceLearningStatusButton;
        }

        if (!_deviceLearningStatusSubscribed)
        {
            _app.DeviceSupportStatusChanged += DeviceLearningStatusChanged;
            Closed += (_, _) =>
            {
                if (_deviceLearningStatusSubscribed)
                    _app.DeviceSupportStatusChanged -= DeviceLearningStatusChanged;
                _deviceLearningStatusSubscribed = false;
            };
            _deviceLearningStatusSubscribed = true;
        }

        RefreshDeviceLearningIndicator();
    }

    private void DeviceLearningStatusChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RefreshDeviceLearningIndicator);
            return;
        }
        RefreshDeviceLearningIndicator();
    }

    private void RefreshDeviceLearningIndicator()
    {
        if (_deviceLearningStatusButton is null)
            return;

        DeviceSupportStatus status = _app.DeviceSupportStatus;
        bool visible = status.Phase is DeviceSupportPhase.Learning or DeviceSupportPhase.ReadyToShare;
        _deviceLearningStatusButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (_deviceLearningBaseLabel is not null)
            _deviceLearningBaseLabel.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;

        if (!visible)
            return;

        if (status.Phase == DeviceSupportPhase.ReadyToShare)
        {
            _deviceLearningStatusButton.Content = "Report ready";
            _deviceLearningStatusButton.ToolTip = "Background compatibility learning is complete. Open Settings to review the redacted report; nothing is uploaded automatically.";
            _deviceLearningStatusButton.SetResourceReference(Control.ForegroundProperty, "Tc.Accent");
            return;
        }

        int completed = Math.Max(0, status.CompletedChecks);
        int total = Math.Max(1, status.TotalChecks);
        _deviceLearningStatusButton.Content = $"New device · {completed}/{total}";
        _deviceLearningStatusButton.ToolTip = "ThinkControl is learning provider and control compatibility in the background while you use the laptop normally. Nothing is uploaded automatically.";
        _deviceLearningStatusButton.SetResourceReference(Control.ForegroundProperty, "Tc.TextMuted");
    }
}