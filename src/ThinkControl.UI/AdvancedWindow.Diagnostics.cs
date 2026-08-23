namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        ConfigureBatteryPage();
        DiagnosticsPanelControl?.Refresh();
    }
}
