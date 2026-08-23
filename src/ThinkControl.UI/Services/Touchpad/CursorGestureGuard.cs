namespace ThinkControl.UI.Services.Touchpad;

internal sealed class CursorGestureGuard : IDisposable
{
    private bool _captured;
    private bool _previousClipKnown;
    private TouchpadNativeMethods.Rect _previousClip;

    internal bool IsCaptured => _captured;

    internal bool CaptureAtCurrentPosition()
    {
        if (_captured)
            return true;

        if (!TouchpadNativeMethods.GetCursorPos(out TouchpadNativeMethods.Point point))
            return false;

        _previousClipKnown = TouchpadNativeMethods.GetClipCursor(out _previousClip);
        var lockRect = new TouchpadNativeMethods.Rect
        {
            Left = point.X,
            Top = point.Y,
            Right = point.X + 1,
            Bottom = point.Y + 1
        };

        if (!TouchpadNativeMethods.ClipCursor(ref lockRect))
            return false;

        _captured = true;
        return true;
    }

    internal void Release()
    {
        if (_captured)
        {
            if (_previousClipKnown)
            {
                TouchpadNativeMethods.Rect restore = _previousClip;
                TouchpadNativeMethods.ClipCursor(ref restore);
            }
            else
            {
                TouchpadNativeMethods.ClipCursor(IntPtr.Zero);
            }
        }

        _captured = false;
        _previousClipKnown = false;
    }

    public void Dispose() => Release();
}
