using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string DeviceLearningStatusResourceKey = "ThinkControl.DeviceLearningStatus";
    private Button? _deviceLearningStatusButton;
    private bool _deviceLearningStatusSubscribed;

    private void ConfigureDeviceLearningIndicator()
    {
        if (_deviceLearningStatusButton is null)
        {
            if (NavHome.Parent is not StackPanel navStack)
                return;

            Grid? utilityRow = navStack.Children
                .OfType<Grid>()
                .FirstOrDefault(grid => Equals(grid.Tag, "ThinkControl.UtilityRow"));
            if (utilityRow is null)
                return;

            _deviceLearningStatusButton = new Button
            {
                Tag = DeviceLearningStatusResourceKey,
                Style = TryFindResource("TcInlineButton") as Style,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 3, 4, 3),
                Margin = new Thickness(17, 0, 12, 6),
                FontSize = TypographyScale.Caption,
                MaxWidth = 158,
                Visibility = Visibility.Collapsed,
                ToolTip = "Compatibility learning runs quietly in the background while you use ThinkControl. Nothing is uploaded automatically."
            };
            _deviceLearningStatusButton.Click += (_, _) => Navigate("Settings");

            int utilityIndex = navStack.Children.IndexOf(utilityRow);
            navStack.Children.Insert(
                Math.Min(navStack.Children.Count, utilityIndex + 1),
                _deviceLearningStatusButton);
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
