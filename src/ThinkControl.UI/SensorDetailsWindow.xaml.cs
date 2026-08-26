using System.Windows;

namespace ThinkControl.UI;

public partial class SensorDetailsWindow : Window
{
    public SensorDetailsWindow(App app)
    {
        InitializeComponent();
        DataContext = app.State;
    }

    internal void PrepareForSnapshot(ThinkControl.UI.ViewModels.AppState state) =>
        SensorsPanelControl.PrepareForSnapshot(state);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
