using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Cooling;
using ThinkControl.UI;

namespace ThinkControl.ShellSmoke;

internal static partial class Program
{
    private static void ValidateHistoryManagement(App app, AdvancedWindow window)
    {
        window.Navigate("Battery");
        Pump(app.Dispatcher);
        var panel = (ThinkControl.UI.Controls.BatteryTelemetryPanel)window.FindName("BatteryTelemetryPanelControl");
        var settings = (Border)panel.FindName("HistorySettings");
        InvokeButton((Button)panel.FindName("ManageHistoryButton"));
        Pump(app.Dispatcher);
        var scroll = (ScrollViewer)window.FindName("PageBattery");
        Point position = settings.TranslatePoint(new Point(), scroll);
        if (settings.Visibility != Visibility.Visible || position.Y < -1 || position.Y >= scroll.ViewportHeight)
            throw new InvalidOperationException("History settings opened outside the visible battery viewport.");
        window.Navigate("Home");
        window.Navigate("Battery");
        Pump(app.Dispatcher);
        if (settings.Visibility != Visibility.Collapsed || scroll.VerticalOffset > 1)
            throw new InvalidOperationException("Reopening Battery retained the management surface or scroll position.");
    }

    private static void ValidateCurveInspection(App app)
    {
        var graph = new FanCurveGraph { IsReadOnly = true, Width = 400, Height = 180 };
        graph.SetCurve([new(FanCurveGraphPolicy.MinTemperatureC, 20), new((FanCurveGraphPolicy.MinTemperatureC + FanCurveGraphPolicy.MaxTemperatureC) / 2, 60), new(FanCurveGraphPolicy.MaxTemperatureC, 100)]);
        int edits = 0;
        graph.CurveChanged += (_, _) => edits++;
        var window = new Window { Content = graph, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false };
        window.Show();
        try
        {
            Pump(app.Dispatcher);
            // These are real plot coordinates: left 46, width ActualWidth - 62.
            Point middle = new(46 + (graph.ActualWidth - 62) / 2, 45);
            if (!ReferenceEquals(graph.InputHitTest(middle), graph))
                throw new InvalidOperationException("Curve plot does not receive pointer input.");
            graph.InspectAt(middle);
            if (graph.HoveredPoint is not { Percent: 60 } || edits != 0)
                throw new InvalidOperationException("Read-only curve inspection failed to interpolate without editing.");
            graph.InspectAt(new Point(-1, -1));
            if (graph.HoveredPoint is not null || edits != 0)
                throw new InvalidOperationException("Curve inspection did not clear outside the plot.");
        }
        finally { window.Close(); }
    }
}

