using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _snapshotUiPrepared;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        ConfigureAdvancedBranding();
        AdvancedWindowEnhancer.Ensure(this, _app);
        AdvancedFeaturePages.Ensure(this, _app);
        ConfigureAdvancedUiConsistency();
        ConfigureInteractionPolish();
        ConfigureResetDefaults();
        ConfigureSliderCommitBehavior();
        ConfigureCopyPolish();
        ConfigureBatteryPage();
        ConfigureHardwareSetupEntry();
        ConfigureNavigationPolish();
        ConfigureTouchpadPolish();
        ConfigureWindowsSettingsLinks();
        ConfigureNotificationButton();
        ConfigureNotificationMessagePolish();
        ConfigureSupportCard();
        ConfigureHomeQuickControls();
        ConfigureUpdateUi();
        ConfigureAppPreferencesUi();
        // Page builders above may replace a ScrollViewer child. Reapply only the
        // bounded page-rail contract after final composition so every page ends on
        // the same sidebar-adjacent left anchor.
        ConfigureAdvancedUiConsistency();
        DiagnosticsPanelControl?.Refresh();
    }

    public void PrepareEnhancedUiForSnapshot()
    {
        _snapshotUiPrepared = true;
        ConfigureAdvancedBranding();
        AdvancedWindowEnhancer.Ensure(this, _app);
        AdvancedFeaturePages.Ensure(this, _app);
        ConfigureAdvancedUiConsistency();
        ConfigureInteractionPolish();
        ConfigureResetDefaults();
        ConfigureSliderCommitBehavior();
        ConfigureCopyPolish();
        ConfigureBatteryPage();
        ConfigureHardwareSetupEntry();
        ConfigureNavigationPolish();
        ConfigureTouchpadPolish();
        ConfigureWindowsSettingsLinks();
        ConfigureNotificationButton();
        ConfigureNotificationMessagePolish();
        ConfigureSupportCard();
        ConfigureHomeQuickControls();
        ConfigureUpdateUi();
        ConfigureAppPreferencesUi();
        ConfigureAdvancedUiConsistency();
        DiagnosticsPanelControl?.Refresh();

        if (DataContext is ViewModels.AppState snapshotState)
        {
            // Healthy visual-QA states should exercise both sides of optional battery
            // temperature formatting. Provider-unavailable states keep null/Not exposed.
            if (snapshotState.CanSensorTelemetry && snapshotState.BatteryTemperatureC is null)
                snapshotState.BatteryTemperatureC = 34.8;

            if (PageFans?.Content is Controls.FansPanel fansPanel)
                fansPanel.PrepareForSnapshot(snapshotState);

            if (PageBattery?.Content is Panel batteryContent &&
                batteryContent.Children.OfType<Controls.BatteryTelemetryPanel>().FirstOrDefault() is { } batteryPanel)
            {
                batteryPanel.PrepareForSnapshot(snapshotState);
            }

            const string sensorsPageKey = "ThinkControl.Dynamic.PageSensors";
            if (Resources.Contains(sensorsPageKey) &&
                Resources[sensorsPageKey] is ScrollViewer { Content: Controls.SensorsPanel sensorsPanel })
            {
                sensorsPanel.PrepareForSnapshot(snapshotState);
            }
        }

        ValidateSharedPageRailForSnapshot();
    }

    private void ValidateSharedPageRailForSnapshot()
    {
        var pages = new List<ScrollViewer>();
        foreach (string pageName in ConsistentPageNames)
        {
            if (FindName(pageName) is ScrollViewer scroll)
                pages.Add(scroll);
        }

        foreach (string resourceName in DynamicPageResourceNames)
        {
            if (Resources.Contains(resourceName) && Resources[resourceName] is ScrollViewer scroll)
                pages.Add(scroll);
        }

        foreach (ScrollViewer scroll in pages)
        {
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
        ConfigureAdvancedBranding();
        AdvancedWindowEnhancer.Ensure(this, _app);
        AdvancedFeaturePages.Ensure(this, _app);
        ConfigureAdvancedUiConsistency();
        ConfigureInteractionPolish();
        ConfigureResetDefaults();
        ConfigureSliderCommitBehavior();
        ConfigureCopyPolish();
        ConfigureNavigationPolish();
        ConfigureTouchpadPolish();
        ConfigureWindowsSettingsLinks();
        ConfigureNotificationButton();
        ConfigureSupportCard();
        ConfigureAdvancedUiConsistency();
        AdvancedWindowEnhancer.SelectTouchpad(this);

        if (_snapshotUiPrepared)
        {
            RevealDynamicPageForSnapshot("ThinkControl.Dynamic.PageTouchpad");
            PrepareTouchpadForSnapshot();
        }
    }

    private void PrepareTouchpadForSnapshot()
    {
        const string touchpadPageKey = "ThinkControl.Dynamic.PageTouchpad";
        if (!Resources.Contains(touchpadPageKey) ||
            Resources[touchpadPageKey] is not ScrollViewer { Content: Controls.TouchpadPanel panel })
        {
            throw new InvalidOperationException("Touchpad page could not be prepared for visual QA.");
        }

        // The normal and minimum renders preserve the resting layout; the wide
        // render exercises the transient contact/trail/value feedback state that
        // otherwise cannot be caught by a static screenshot gate.
        panel.PrepareForSnapshot(showActiveGesture: Width >= 1500);
    }

    public void NavigateSensors()
    {
        ConfigureAdvancedBranding();
        AdvancedWindowEnhancer.Ensure(this, _app);
        AdvancedFeaturePages.Ensure(this, _app);
        ConfigureAdvancedUiConsistency();
        ConfigureInteractionPolish();
        ConfigureResetDefaults();
        ConfigureSliderCommitBehavior();
        ConfigureCopyPolish();
        ConfigureNavigationPolish();
        ConfigureTouchpadPolish();
        ConfigureWindowsSettingsLinks();
        ConfigureNotificationButton();
        ConfigureSupportCard();
        ConfigureAdvancedUiConsistency();
        AdvancedWindowEnhancer.SelectSensors(this);
        if (_snapshotUiPrepared)
            RevealDynamicPageForSnapshot("ThinkControl.Dynamic.PageSensors");
    }

    private void RevealDynamicPageForSnapshot(string resourceKey)
    {
        if (!Resources.Contains(resourceKey) || Resources[resourceKey] is not FrameworkElement page)
            throw new InvalidOperationException($"Dynamic page '{resourceKey}' is unavailable for visual QA.");
        page.BeginAnimation(UIElement.OpacityProperty, null);
        page.Opacity = 1;
        page.Visibility = Visibility.Visible;
    }

    public void NavigateAudio()
    {
        ConfigureAdvancedBranding();
        AdvancedWindowEnhancer.Ensure(this, _app);
        AdvancedFeaturePages.Ensure(this, _app);
        ConfigureAdvancedUiConsistency();
        ConfigureInteractionPolish();
        ConfigureResetDefaults();
        ConfigureSliderCommitBehavior();
        ConfigureCopyPolish();
        ConfigureNavigationPolish();
        ConfigureTouchpadPolish();
        ConfigureWindowsSettingsLinks();
        ConfigureNotificationButton();
        ConfigureSupportCard();
        ConfigureAdvancedUiConsistency();
        AdvancedFeaturePages.SelectAudio(this);
    }
}
