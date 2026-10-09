using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ThinkControl.UI;
using ThinkControl.UI.Controls;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.ShellSmoke;

internal static partial class Program
{
    private static void ValidateSelectionAndMeter(App app)
    {
        foreach (bool editable in new[] { false, true })
        {
            var combo = new ComboBox { Width = 240, IsEditable = editable,
                Style = (Style)app.FindResource("TcComboBox"), ItemsSource = new[] { "One", "Two" }, SelectedIndex = 0 };
            var host = new Window { Content = combo, Width = 300, Height = 120, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                host.Show(); host.UpdateLayout();
                var chevron = (FrameworkElement)combo.Template.FindName("Chevron", combo);
                Rect Bounds() => chevron.TransformToAncestor(combo).TransformBounds(new Rect(0, 0, chevron.ActualWidth, chevron.ActualHeight));
                var closed = Bounds();
                combo.IsDropDownOpen = true; host.UpdateLayout();
                var opened = Bounds();
                if (Math.Abs(closed.X - opened.X) > 0.1 || Math.Abs(closed.Y - opened.Y) > 0.1)
                    throw new InvalidOperationException("Shared dropdown chevron moved when opened.");
                combo.IsDropDownOpen = false;
            }
            finally { host.Close(); }
        }
        var gauge = new BatteryProtectionGauge { ProtectionEnabled = true, StartPercent = 80, StopPercent = 85,
            CurrentPercent = 82, IsPluggedIn = true, IsCharging = true };
        if (gauge.ActiveBoundaryPercent != 85) throw new InvalidOperationException("Charging did not target the stop boundary.");
        byte[] Pixels(double phase)
        {
            gauge.FlowPhase = phase;
            gauge.Measure(new Size(320, 64)); gauge.Arrange(new Rect(0, 0, 320, 64)); gauge.UpdateLayout();
            var bitmap = new RenderTargetBitmap(320, 64, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(gauge);
            var bytes = new byte[320 * 64 * 4]; bitmap.CopyPixels(bytes, 320 * 4, 0);
            return bytes;
        }
        if (Pixels(0).SequenceEqual(Pixels(4))) throw new InvalidOperationException("Charging boundary did not react to the shared fade phase.");
        gauge.IsCharging = false;
        if (gauge.ActiveBoundaryPercent != 80) throw new InvalidOperationException("Plugged-in hold did not identify the resume boundary.");
        if (!Pixels(0).SequenceEqual(Pixels(4))) throw new InvalidOperationException("Holding preservation falsely animated charging.");
        gauge.IsPluggedIn = false;
        if (gauge.ActiveBoundaryPercent is not null) throw new InvalidOperationException("Unplugged preservation showed a charging target.");
        if (!Pixels(0).SequenceEqual(Pixels(4))) throw new InvalidOperationException("Unplugged preservation animated a charging boundary.");
        gauge.ProtectionEnabled = false; gauge.IsPluggedIn = true;
        if (gauge.ActiveBoundaryPercent is not null) throw new InvalidOperationException("Disabled preservation showed an active threshold.");
        var state = new AppState { BatteryStatus = "Plugged in" };
        if (!state.BatteryPluggedIn) throw new InvalidOperationException("Plugged-in hold was mistaken for unplugged discharge.");
        state.BatteryStatus = "On battery";
        if (state.BatteryPluggedIn) throw new InvalidOperationException("Battery-only state retained external power.");
        Console.WriteLine("Shared dropdown geometry and charging/hold/unplugged meter boundaries passed.");
    }
}
