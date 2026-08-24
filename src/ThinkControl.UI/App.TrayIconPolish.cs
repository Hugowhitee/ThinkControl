using System.Drawing.Drawing2D;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingIcon = System.Drawing.Icon;
using DrawingPen = System.Drawing.Pen;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace ThinkControl.UI;

public partial class App
{
    private DrawingIcon? _polishedTrayIcon;

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

    private static DrawingIcon CreateTrayMark()
    {
        // Notification-area icons are usually rendered near 16 logical pixels.
        // Use a deliberately bold, almost full-canvas T/C mark and no tiny accent
        // dot; the previous red dot read as a stray notification/error pixel.
        using var bitmap = new DrawingBitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using DrawingGraphics g = DrawingGraphics.FromImage(bitmap);
        g.Clear(DrawingColor.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var light = new DrawingPen(DrawingColor.FromArgb(248, 249, 250), 4.15f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        g.DrawLine(light, 4.5f, 7.1f, 18.8f, 7.1f);
        g.DrawLine(light, 11.6f, 7.1f, 11.6f, 25.2f);
        g.DrawArc(light, new DrawingRectangleF(11.4f, 8.0f, 16.0f, 17.0f), 48f, 264f);

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using DrawingIcon temporary = DrawingIcon.FromHandle(handle);
            return (DrawingIcon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
