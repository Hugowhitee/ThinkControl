using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        AdvancedWindowEnhancer.Ensure(this, _app);
        ConfigureBatteryPage();
        DiagnosticsPanelControl?.Refresh();
    }

    public void PrepareEnhancedUiForSnapshot()
    {
        AdvancedWindowEnhancer.Ensure(this, _app);
    }

    public void NavigateTouchpad()
    {
        AdvancedWindowEnhancer.Ensure(this, _app);
        AdvancedWindowEnhancer.SelectTouchpad(this);
    }
}
