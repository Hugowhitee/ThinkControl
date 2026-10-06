using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI.Controls;

public sealed class PageNavigationEventArgs(string page) : RoutedEventArgs(ContextTabs.NavigationRequestedEvent)
{
    public string Page { get; } = page;
}

public partial class ContextTabs : UserControl
{
    public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(nameof(Section), typeof(string), typeof(ContextTabs), new PropertyMetadata("", Refresh));
    public static readonly DependencyProperty SelectedPageProperty = DependencyProperty.Register(nameof(SelectedPage), typeof(string), typeof(ContextTabs), new PropertyMetadata("", Refresh));
    public static readonly RoutedEvent NavigationRequestedEvent = EventManager.RegisterRoutedEvent("NavigationRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ContextTabs));
    public string Section { get => (string)GetValue(SectionProperty); set => SetValue(SectionProperty, value); }
    public string SelectedPage { get => (string)GetValue(SelectedPageProperty); set => SetValue(SelectedPageProperty, value); }
    public ContextTabs() { InitializeComponent(); UpdateTabs(); }
    private static void Refresh(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ContextTabs)d).UpdateTabs();
    private void UpdateTabs()
    {
        if (Tabs is null) return;
        (string Label, string Page)[] items = Section switch
        {
            "DisplayInput" => [("Display", "Display"), ("Keyboard", "Keyboard"), ("Touchpad", "Touchpad")],
            "Modes" => [("Saved modes", "Modes"), ("Automation", "Automation")],
            "System" => [("General", "System"), ("Updates", "Updates"), ("Diagnostics", "Diagnostics")],
            _ => []
        };
        Tabs.ItemsSource = items.Select(item => new { item.Label, item.Page, Selected = item.Page == SelectedPage });
    }
    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string page }) RaiseEvent(new PageNavigationEventArgs(page));
    }
}
