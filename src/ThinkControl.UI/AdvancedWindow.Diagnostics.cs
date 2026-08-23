using System.Windows;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private DiagnosticsPanel? _diagnosticsPanel;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        EnsureDiagnosticsPanel();
    }

    private void EnsureDiagnosticsPanel()
    {
        if (_diagnosticsPanel is not null)
        {
            _diagnosticsPanel.Refresh();
            return;
        }

        if (PageSettings.Content is not System.Windows.Controls.StackPanel settingsRoot)
            return;

        _diagnosticsPanel = new DiagnosticsPanel
        {
            Margin = new Thickness(0, 14, 0, 0)
        };
        settingsRoot.Children.Add(_diagnosticsPanel);
    }
}
