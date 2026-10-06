namespace ThinkControl.UI;

public partial class App
{
    internal void OpenCompactCustomize(System.Windows.Window owner)
    {
        if (IsVisualQa)
        {
            var preview = new MainWindow(this) { Owner = owner, DataContext = State, Topmost = false };
            preview.Show();
            preview.OpenCustomization();
            return;
        }
        SwitchAdvancedToCompact();
        CompactWindow.OpenCustomization();
    }
}

public partial class MainWindow
{
    internal void OpenCustomization() => Dashboard.OpenCustomization();
}
