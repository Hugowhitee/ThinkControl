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
        ConfigureResetDefaults();
        ConfigureSliderCommitBehavior();
        ConfigureCopyPolish();
        ConfigureBatteryPage();
        ConfigureHardwareSetupEntry();
        DiagnosticsPanelControl?.Refresh();
    }

    public void PrepareEnhancedUiForSnapshot()
    {
        ConfigureAdvancedBranding();
        AdvancedWindowEnhancer.Ensure(this, _app);
        AdvancedFeaturePages.Ensure(this, _app);
        ConfigureResetDefaults();
        ConfigureSliderCommitBehavior();
        ConfigureCopyPolish();
        ConfigureBatteryPage();
        ConfigureHardwareSetupEntry();
        DiagnosticsPanelControl?.Refresh();

        // Dynamic feature panels normally subscribe to the live app/service state.
        // Snapshot windows deliberately use a deterministic AppState instead, so
        // prepare the fan panel from that state to exercise the active layout
        // rather than accidentally capturing an all-unavailable service state.
        if (DataContext is ViewModels.AppState snapshotState &&
            PageFans?.Content is Controls.FansPanel fansPanel)
        {
            fansPanel.PrepareForSnapshot(snapshotState);
        }
    }

    public void NavigateTouchpad()
    {
        ConfigureAdvancedBranding();
        AdvancedWindowEnhancer.Ensure(this, _app);
        AdvancedFeaturePages.Ensure(this, _app);
        ConfigureResetDefaults();
        ConfigureSliderCommitBehavior();
        ConfigureCopyPolish();
        AdvancedWindowEnhancer.SelectTouchpad(this);
    }

    public void NavigateSensors()
    {
        ConfigureAdvancedBranding();
        AdvancedWindowEnhancer.Ensure(this, _app);
        AdvancedFeaturePages.Ensure(this, _app);
        ConfigureResetDefaults();
        ConfigureSliderCommitBehavior();
        ConfigureCopyPolish();
        AdvancedWindowEnhancer.SelectSensors(this);
    }

    public void NavigateAudio()
    {
        ConfigureAdvancedBranding();
        AdvancedWindowEnhancer.Ensure(this, _app);
        AdvancedFeaturePages.Ensure(this, _app);
        ConfigureResetDefaults();
        ConfigureSliderCommitBehavior();
        ConfigureCopyPolish();
        AdvancedFeaturePages.SelectAudio(this);
    }
}
