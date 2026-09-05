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

            // The deterministic hardware-ready fixture represents the currently
            // reviewed direct keyboard provider. Make its effect capability explicit
            // so Keyboard screenshots validate the capability-driven enabled state.
            if (snapshotState.CanKeyboardBacklight &&
                !snapshotState.KeyboardBackend.Equals("Not exposed", StringComparison.OrdinalIgnoreCase))
            {
                snapshotState.CanKeyboardEffects = true;
            }

            // Keep the baseline fan snapshot faithful to the supplied provider/profile
            // state. Special fixtures (manual OEM target, etc.) are applied explicitly
            // by the snapshot renderer after navigation so one state cannot silently
            // replace another merely because both happen to use the Balanced profile.
            FansPanelControl.PrepareForSnapshot(snapshotState);

            // The normal demo fixture starts in firmware Auto with the reviewed
            // discrete provider available. Exercise the generic calibration-required
            // capability in visual QA without teaching production code about X9/model
            // names or inferring anything from the HardwareAccess display string.
            if (snapshotState.CanFanControl &&
                snapshotState.CanFanTelemetry &&
                (snapshotState.CoolingProfile.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ||
                 snapshotState.CoolingProfile.Equals("Auto", StringComparison.OrdinalIgnoreCase)))
            {
                FansPanelControl.PrepareCalibrationRequiredForSnapshot();
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
        // The left pair covers the normal launch state. The mirrored right pair is
        // prepared directly as reverse-close so its live trail cannot inherit the
        // inward fixture that would otherwise have been drawn moments earlier.
        if (corner == TouchpadCorner.TopRight)
            TouchpadPanelControl.PrepareReverseCornerForSnapshot(corner, live);
        else
            TouchpadPanelControl.PrepareCornerForSnapshot(corner, live);

        TouchpadPanelControl.PrepareHapticsForSnapshot();
        TouchpadPanelControl.ValidateCornerEditorLayoutForSnapshot(corner, live);
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
