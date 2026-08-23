namespace ThinkControl.UI.Services.Touchpad;

internal sealed class CursorGestureGuard : IDisposable
{
    private bool _captured;
    private bool _previousClipKnown;
    private TouchpadNativeMethods.Rect _previousClip;
    private int _visibilityAdjustments;

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

    internal void HideCursor()
    {
        if (!_captured || _visibilityAdjustments > 0)
            return;

        // ShowCursor uses a process-global display counter. Record exactly how many
        // decrements we perform so cleanup restores the previous counter instead of
        // blindly forcing a visibility state.
        int result;
        do
        {
            result = TouchpadNativeMethods.ShowCursor(false);
            _visibilityAdjustments++;
        }
        while (result >= 0 && _visibilityAdjustments < 32);
    }

    internal void Release()
    {
        for (int i = 0; i < _visibilityAdjustments; i++)
            TouchpadNativeMethods.ShowCursor(true);
        _visibilityAdjustments = 0;

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
