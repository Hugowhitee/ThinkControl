namespace ThinkControl.UI;

public partial class App
{
    internal void OpenCompactCustomize(System.Windows.Window owner)
    {
        CompactWindow ??= new MainWindow(this) { DataContext = State, Topmost = false };
        CompactWindow.OpenLayoutEditor(owner);
    }
}

public partial class MainWindow
{
    internal void OpenCustomization() => Dashboard.OpenCustomization();
    internal void OpenLayoutEditor(System.Windows.Window owner) => Dashboard.OpenLayoutEditor(owner);
    internal System.Windows.Window CreateLayoutEditorForSnapshot()
    {
        Dashboard.OpenLayoutEditor(null);
        return Dashboard.LayoutEditorForSnapshot!;
    }
}
