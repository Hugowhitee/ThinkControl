using System.Drawing;
using System.Drawing.Drawing2D;

namespace ThinkControl.UI;

public partial class App
{
    private Icon? _polishedTrayIcon;

    private void ApplyTrayIconPolish()
    {
        if (_trayIcon is null)
            return;

        try
        {
            _polishedTrayIcon?.Dispose();
            _polishedTrayIcon = CreateTrayMark();
            _trayIcon.Icon = _polishedTrayIcon;
        }
        catch
        {
            // The packaged icon remains a safe fallback.
        }
    }

    private static Icon CreateTrayMark()
    {
        // The full application artwork is intentionally not squeezed into 16 px:
        // Explorer's hidden-icons flyout made it look like a striped/corrupt blob.
        // This tray-only reduction keeps the ThinkControl T/C + red TrackPoint cue.
        using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var light = new Pen(Color.FromArgb(242, 243, 244), 3.2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var red = new SolidBrush(Color.FromArgb(227, 41, 41));

        g.DrawLine(light, 6.5f, 8.5f, 17.5f, 8.5f);
        g.DrawLine(light, 12f, 8.5f, 12f, 23.5f);
        g.DrawArc(light, new RectangleF(11.5f, 9f, 13.5f, 14.5f), 52f, 256f);
        g.FillEllipse(red, 22.2f, 5.2f, 4.7f, 4.7f);

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
