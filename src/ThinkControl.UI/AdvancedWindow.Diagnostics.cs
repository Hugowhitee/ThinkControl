using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        ConfigureAdvancedBranding();
        AdvancedWindowEnhancer.Ensure(this, _app);
        ConfigureSliderCommitBehavior();
        ConfigureBatteryPage();
        ConfigureHardwareSetupEntry();
        DiagnosticsPanelControl?.Refresh();
    }

    public void PrepareEnhancedUiForSnapshot()
    {
        ConfigureAdvancedBranding();
        AdvancedWindowEnhancer.Ensure(this, _app);
        ConfigureSliderCommitBehavior();
        ConfigureBatteryPage();
        ConfigureHardwareSetupEntry();
        DiagnosticsPanelControl?.Refresh();
    }

    public void NavigateTouchpad()
    {
        ConfigureAdvancedBranding();
        AdvancedWindowEnhancer.Ensure(this, _app);
        ConfigureSliderCommitBehavior();
        AdvancedWindowEnhancer.SelectTouchpad(this);
    }
}
