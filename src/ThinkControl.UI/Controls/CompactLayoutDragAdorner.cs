using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;

namespace ThinkControl.UI.Controls;

// One image of the actual tile follows the pointer for this drag only.
internal sealed class CompactLayoutDragAdorner : Adorner
{
    private readonly ImageSource _image;
    private readonly Size _size;
    private Point _position;

    internal CompactLayoutDragAdorner(FrameworkElement surface, FrameworkElement tile) : base(surface)
    {
        IsHitTestVisible = false;
        _size = tile.RenderSize;
        var dpi = VisualTreeHelper.GetDpi(tile);
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(_size.Width * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(_size.Height * dpi.DpiScaleY)), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(tile);
        bitmap.Freeze();
        _image = bitmap;
        FollowPointer();
    }

    internal void FollowPointer()
    {
        if (!GetCursorPos(out var point)) return;
        _position = AdornedElement.PointFromScreen(new Point(point.X, point.Y));
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.PushOpacity(.9);
        drawingContext.DrawImage(_image, new Rect(new Point(_position.X + 12, _position.Y + 12), _size));
        drawingContext.Pop();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint { public int X; public int Y; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorPoint point);
}
