using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
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
        ConfigureNotificationButton();
        ConfigureSupportCard();
        DiagnosticsPanelControl?.Refresh();
    }

    public void PrepareEnhancedUiForSnapshot()
    {
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
        ConfigureNotificationButton();
        ConfigureSupportCard();
        DiagnosticsPanelControl?.Refresh();

        if (DataContext is ViewModels.AppState snapshotState)
        {
            if (PageFans?.Content is Controls.FansPanel fansPanel)
                fansPanel.PrepareForSnapshot(snapshotState);

            const string sensorsPageKey = "ThinkControl.Dynamic.PageSensors";
            if (Resources.Contains(sensorsPageKey) &&
                Resources[sensorsPageKey] is System.Windows.Controls.ScrollViewer { Content: Controls.SensorsPanel sensorsPanel })
            {
                sensorsPanel.PrepareForSnapshot(snapshotState);
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
        ConfigureNotificationButton();
        ConfigureSupportCard();
        AdvancedWindowEnhancer.SelectTouchpad(this);
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
        ConfigureNotificationButton();
        ConfigureSupportCard();
        AdvancedWindowEnhancer.SelectSensors(this);
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
        ConfigureNotificationButton();
        ConfigureSupportCard();
        AdvancedFeaturePages.SelectAudio(this);
    }
}
