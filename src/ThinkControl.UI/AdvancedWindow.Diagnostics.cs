using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _snapshotUiPrepared;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        ConfigureAdvancedSurface();
    }

    private void ConfigureAdvancedSurface()
    {
        InitializeFeaturePanels();
        ConfigureAdvancedBranding();
        ConfigureAdvancedUiConsistency();
        ConfigureInteractionPolish();
        ConfigureResetDefaults();
        ConfigureSliderCommitBehavior();
        ConfigureCopyPolish();
        ConfigureBatteryPage();
        ConfigureHardwareSetupEntry();
        ConfigureTouchpadPolish();
        ConfigureWindowsSettingsLinks();
        ConfigureKeyboardAutoUi();
        ConfigureNotificationButton();
        ConfigureShellUtilitySizing();
        ConfigureNotificationMessagePolish();
        ConfigureSupportCard();
        ConfigureHomeQuickControls();
        ConfigureHomeDashboardPolish();
        ConfigureUpdateUi();
        ConfigureAppPreferencesUi();
        ConfigureSettingsHierarchy();
        ConfigureAdvancedUiConsistency();
        DiagnosticsPanelControl?.Refresh();
    }

    public void PrepareEnhancedUiForSnapshot()
    {
        _snapshotUiPrepared = true;
        ConfigureAdvancedSurface();
        SyncControls();

        if (DataContext is ViewModels.AppState snapshotState)
        {
            if (snapshotState.CanSensorTelemetry && snapshotState.BatteryTemperatureC is null)
                snapshotState.BatteryTemperatureC = 34.8;

            FansPanelControl.PrepareForSnapshot(snapshotState);

            // The dedicated active-curve fixture is the one visual-QA fan state
            // that pins Balanced explicitly. Reuse it to show the production
            // temporary manual-test safety controls as well, without starting a
            // timer or touching hardware during screenshot generation.
            if (snapshotState.CanFanControl &&
                string.Equals(snapshotState.CoolingProfile, "Balanced", StringComparison.OrdinalIgnoreCase))
            {
                FansPanelControl.PrepareManualFanTestForSnapshot();
            }

            if (PageBattery?.Content is Panel batteryContent &&
                batteryContent.Children.OfType<Controls.BatteryTelemetryPanel>().FirstOrDefault() is { } batteryPanel)
            {
                batteryPanel.PrepareForSnapshot(snapshotState);
            }
        }

        ValidateSharedPageRailForSnapshot();
    }

    private void ValidateSharedPageRailForSnapshot()
    {
        foreach (string pageName in ConsistentPageNames)
        {
            if (FindName(pageName) is not ScrollViewer scroll)
                continue;

            if (scroll.HorizontalContentAlignment != HorizontalAlignment.Left)
                throw new InvalidOperationException($"{scroll.Tag ?? scroll.Name} is not left-anchored to the shared Advanced page rail.");

            if (scroll.Content is not FrameworkElement content)
                continue;

            if (content.HorizontalAlignment != HorizontalAlignment.Left ||
                Math.Abs(content.MaxWidth - AdvancedContentMaxWidth) > 0.1 ||
                Math.Abs(content.Margin.Left) > 0.1)
            {
                throw new InvalidOperationException(
                    $"{scroll.Tag ?? scroll.Name} overrides the shared Advanced page rail. " +
                    "All pages must use the same left anchor and common readable MaxWidth.");
            }
        }
    }

    public void NavigateTouchpad()
    {
        ConfigureAdvancedSurface();
        Navigate("Touchpad");

        if (_snapshotUiPrepared)
            PrepareTouchpadForSnapshot();
    }

    private void PrepareTouchpadForSnapshot()
    {
        TouchpadPanelControl.PrepareForSnapshot(showActiveGesture: Width >= 1500);
        TouchpadPanelControl.PrepareHapticsForSnapshot();
    }

    public void PrepareTouchpadInwardForSnapshot()
    {
        TouchpadPanelControl.PrepareForSnapshot(showActiveGesture: false, showInwardGesture: true);
        TouchpadPanelControl.PrepareHapticsForSnapshot();
    }

    public void PrepareTouchpadCornerForSnapshot(TouchpadCorner corner, bool live)
    {
        TouchpadPanelControl.PrepareCornerForSnapshot(corner, live);
        TouchpadPanelControl.PrepareHapticsForSnapshot();
    }

    public void ValidateTouchpadCornerSymmetryForSnapshot() =>
        TouchpadPanelControl.ValidateCornerSymmetryForSnapshot();

    public void PreparePerformanceForSnapshot() =>
        PerformancePanelControl.PrepareForSnapshot();

    public void ExpandBatteryHistoryForSnapshot()
    {
        if (PageBattery?.Content is Panel batteryContent &&
            batteryContent.Children.OfType<Controls.BatteryTelemetryPanel>().FirstOrDefault() is { } batteryPanel)
        {
            batteryPanel.ExpandSnapshotHistory();
            PageBattery.UpdateLayout();
            PageBattery.ScrollToEnd();
            PageBattery.UpdateLayout();
        }
    }

    public void NavigateAudio()
    {
        ConfigureAdvancedSurface();
        Navigate("Audio");
    }
}
