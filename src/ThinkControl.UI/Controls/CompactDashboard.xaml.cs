using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard : UserControl
{
    private App? _app;

    public CompactDashboard()
    {
        InitializeComponent();
    }

    internal void Initialize(App app)
    {
        _app = app;
        EnsureAudioRow();
    }

    private void Expand_Click(object sender, RoutedEventArgs e) => _app?.OpenAdvanced("Home");

    private void Hide_Click(object sender, RoutedEventArgs e) => _app?.CompactWindow.HideAnimated();

    private void OpenPage_Click(object sender, RoutedEventArgs e)
    {
        if (_app is not null && sender is FrameworkElement { Tag: string page })
            _app.OpenAdvanced(page);
    }

    private void Battery_Click(object sender, MouseButtonEventArgs e) => _app?.OpenAdvanced("Battery");

    private void Performance_Click(object sender, MouseButtonEventArgs e) => _app?.OpenAdvanced("Performance");

    private void Fans_Click(object sender, MouseButtonEventArgs e) => _app?.OpenAdvanced("Fans");
}
