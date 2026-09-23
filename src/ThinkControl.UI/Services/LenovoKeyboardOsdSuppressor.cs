using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ThinkControl.UI.Services;

/// <summary>
/// Hides only Lenovo's keyboard-backlight OSD window created by ThinkControl's own
/// automatic effect writes. The baseline is captured before the first write in a
/// burst and the short watch window is extended by subsequent effect writes.
/// User-triggered Fn+Space outside an effect burst is untouched.
/// </summary>
internal sealed class LenovoKeyboardOsdSuppressor : IDisposable
{
    private static readonly TimeSpan WatchWindow = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan ProcessCacheLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WatchInterval = TimeSpan.FromMilliseconds(4);

    private readonly object _gate = new();
    private CancellationTokenSource? _watchCts;
    private Task? _watchTask;
    private HashSet<IntPtr> _baseline = [];
    private int? _tposdPid;
    private DateTimeOffset _pidCheckedAt = DateTimeOffset.MinValue;
    private DateTimeOffset _deadline = DateTimeOffset.MinValue;
    private bool _disposed;

    internal void Arm()
    {
        if (_disposed)
            return;

        int? pid = ResolveTposdPid();
        if (pid is null)
            return;

        lock (_gate)
        {
            if (_disposed)
                return;

            bool running = _watchTask is { IsCompleted: false };
            if (!running)
                _baseline = EnumerateVisibleWindows(pid.Value).ToHashSet();

            _deadline = DateTimeOffset.UtcNow + WatchWindow;
            if (running)
                return;

            _watchCts?.Dispose();
            _watchCts = new CancellationTokenSource();
            CancellationToken token = _watchCts.Token;
            _watchTask = Task.Run(() => WatchAsync(pid.Value, token), token);
        }
    }

    private async Task WatchAsync(int pid, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                DateTimeOffset deadline;
                HashSet<IntPtr> baseline;
                lock (_gate)
                {
                    deadline = _deadline;
                    baseline = _baseline;
                }

                if (DateTimeOffset.UtcNow >= deadline)
                    return;

                foreach (IntPtr hwnd in EnumerateVisibleWindows(pid))
                {
                    if (!baseline.Contains(hwnd))
                        _ = ShowWindow(hwnd, SwHide);
                }

                await Task.Delay(WatchInterval, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_gate)
            {
                if (_watchTask?.IsCompleted != false || DateTimeOffset.UtcNow >= _deadline)
                {
                    _baseline = [];
                    _watchTask = null;
                }
            }
        }
    }

    private int? ResolveTposdPid()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            if (_pidCheckedAt != DateTimeOffset.MinValue &&
                now - _pidCheckedAt < ProcessCacheLifetime)
            {
                return _tposdPid;
            }
        }

        int? pid = null;
        try
        {
            foreach (Process process in Process.GetProcessesByName("tposd"))
            {
                try
                {
                    pid = process.Id;
                    break;
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
        }

        lock (_gate)
        {
            _tposdPid = pid;
            _pidCheckedAt = now;
            return _tposdPid;
        }
    }

    private static IReadOnlyList<IntPtr> EnumerateVisibleWindows(int pid)
    {
        var windows = new List<IntPtr>();
        GCHandle handle = GCHandle.Alloc(windows);
        try
        {
            EnumWindows((hwnd, lParam) =>
            {
                GCHandle listHandle = GCHandle.FromIntPtr(lParam);
                if (listHandle.Target is not List<IntPtr> target)
                    return true;

                _ = GetWindowThreadProcessId(hwnd, out uint windowPid);
                if (windowPid == (uint)pid && IsWindowVisible(hwnd))
                    target.Add(hwnd);
                return true;
            }, GCHandle.ToIntPtr(handle));
        }
        catch
        {
        }
        finally
        {
            if (handle.IsAllocated)
                handle.Free();
        }

        return windows;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _watchCts;
            _watchCts = null;
            _watchTask = null;
            _baseline = [];
        }

        try { cts?.Cancel(); } catch { }
        cts?.Dispose();
    }

    private const int SwHide = 0;

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int command);
}
