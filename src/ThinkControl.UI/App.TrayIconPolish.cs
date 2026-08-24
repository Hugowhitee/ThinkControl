using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ThinkControl.UI.Controls;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingIcon = System.Drawing.Icon;

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
            DrawingIcon replacement = CreateTrayMark();
            DrawingIcon? previous = _polishedTrayIcon;
            _polishedTrayIcon = replacement;
            _trayIcon.Icon = replacement;
            previous?.Dispose();
        }
        catch
        {
            // The packaged icon remains a safe fallback.
        }
    }

    private static DrawingIcon CreateTrayMark()
    {
        // Render the exact same BrandMark control used inside ThinkControl instead
        // of maintaining a second hand-drawn tray logo. Render larger than the final
        // notification-area size and downsample so the filled mark stays bold and
        // clean at 16-24 logical pixels without the historical stray accent dot.
        const int renderSize = 96;
        var mark = new BrandMark
        {
            Width = renderSize,
            Height = renderSize,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        mark.Measure(new Size(renderSize, renderSize));
        mark.Arrange(new Rect(0, 0, renderSize, renderSize));
        mark.UpdateLayout();

        var rendered = new RenderTargetBitmap(
            renderSize,
            renderSize,
            96,
            96,
            PixelFormats.Pbgra32);
        rendered.Render(mark);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rendered));
        using var png = new MemoryStream();
        encoder.Save(png);
        png.Position = 0;

        using var source = new DrawingBitmap(png);
        using var downsampled = new DrawingBitmap(source, new System.Drawing.Size(32, 32));
        IntPtr handle = downsampled.GetHicon();
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
