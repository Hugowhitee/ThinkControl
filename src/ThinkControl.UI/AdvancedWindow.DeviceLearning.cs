using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;
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
            _deviceLearningStatusButton = DeviceLearningIndicator;
            _deviceLearningStatusButton.Click += (_, _) => Navigate("Diagnostics");
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

    public void PrepareDeviceLearningForSnapshot(bool reportReady = false)
    {
        ConfigureDeviceLearningIndicator();
        if (_deviceLearningStatusButton is null)
            throw new InvalidOperationException("Advanced device-learning indicator was not initialized.");

        _deviceLearningStatusButton.Visibility = Visibility.Visible;

        _deviceLearningStatusButton.Content = reportReady ? "Report ready" : "New device: 2/4";
        _deviceLearningStatusButton.SetResourceReference(
            Control.ForegroundProperty,
            reportReady ? "Tc.Accent" : "Tc.TextMuted");
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
        _deviceLearningStatusButton.Content = $"New device: {completed}/{total}";
        _deviceLearningStatusButton.ToolTip = "ThinkControl is learning provider and control compatibility in the background while you use the laptop normally. Nothing is uploaded automatically.";
        _deviceLearningStatusButton.SetResourceReference(Control.ForegroundProperty, "Tc.TextMuted");
    }
}
