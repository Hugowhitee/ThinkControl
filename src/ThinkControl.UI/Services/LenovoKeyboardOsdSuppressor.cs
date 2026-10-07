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
    private static readonly TimeSpan WatchInterval = TimeSpan.FromMilliseconds(16);
    private readonly Func<int?> _resolvePid;
    private readonly Func<int, IReadOnlyList<IntPtr>> _visibleWindows;
    private readonly Action<IntPtr, bool> _setVisible;
    private readonly Func<IntPtr, int, bool> _stillOwned;
    private readonly TimeSpan _watchWindow;

    private readonly object _gate = new();
    private CancellationTokenSource? _watchCts;
    private Task? _watchTask;
    private HashSet<IntPtr> _baseline = [];
    private readonly HashSet<IntPtr> _hidden = [];
    private int _generation;
    private int? _tposdPid;
    private DateTimeOffset _pidCheckedAt = DateTimeOffset.MinValue;
    private DateTimeOffset _deadline = DateTimeOffset.MinValue;
    private bool _disposed;

    internal LenovoKeyboardOsdSuppressor(Func<int?>? resolvePid = null,
        Func<int, IReadOnlyList<IntPtr>>? visibleWindows = null,
        Action<IntPtr, bool>? setVisible = null, Func<IntPtr, int, bool>? stillOwned = null,
        TimeSpan? watchWindow = null)
    {
        _resolvePid = resolvePid ?? ResolveTposdPid;
        _visibleWindows = visibleWindows ?? EnumerateVisibleWindows;
        _setVisible = setVisible ?? ((hwnd, visible) => ShowWindow(hwnd, visible ? SwShowNoActivate : SwHide));
        _stillOwned = stillOwned ?? ((hwnd, pid) =>
        {
            if (!IsWindow(hwnd)) return false;
            _ = GetWindowThreadProcessId(hwnd, out uint actualPid);
            return actualPid == (uint)pid;
        });
        _watchWindow = watchWindow ?? WatchWindow;
    }

    internal void Arm()
    {
        if (_disposed)
            return;

        int? pid = _resolvePid();
        if (pid is null)
            return;

        lock (_gate)
        {
            if (_disposed)
                return;

            bool running = _watchTask is { IsCompleted: false };
            if (!running)
                _baseline = _visibleWindows(pid.Value).ToHashSet();

            _tposdPid = pid;

            _deadline = DateTimeOffset.UtcNow + _watchWindow;
            if (running)
                return;

            _watchCts?.Dispose();
            _watchCts = new CancellationTokenSource();
            CancellationToken token = _watchCts.Token;
            int generation = ++_generation;
            _watchTask = Task.Run(() => WatchAsync(pid.Value, generation, token), token);
        }
    }

    private async Task WatchAsync(int pid, int generation, CancellationToken token)
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
                {
                    // Expiring a burst must release visibility even while Reactive
                    // remains selected. Previously hidden HWNDs lived until mode exit.
                    lock (_gate)
                    {
                        if (generation == _generation && DateTimeOffset.UtcNow >= _deadline)
                            Disarm();
                    }
                    return;
                }

                foreach (IntPtr hwnd in _visibleWindows(pid))
                {
                    if (baseline.Contains(hwnd))
                        continue;
                    lock (_gate)
                    {
                        if (_disposed || token.IsCancellationRequested || generation != _generation)
                            return;
                        _setVisible(hwnd, false);
                        _hidden.Add(hwnd);
                    }
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
                if (generation == _generation)
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

    // Ending experimental effects must restore any Lenovo OSD windows this
    // session hid. Otherwise some tposd builds reuse the hidden HWND for
    // later Fn+Space presses and the popup never becomes visible again.
    internal void Disarm()
    {
        CancellationTokenSource? cts;
        IntPtr[] hidden;
        int? pid;
        lock (_gate)
        {
            ++_generation;
            _deadline = DateTimeOffset.MinValue;
            cts = _watchCts;
            _watchCts = null;
            _watchTask = null;
            _baseline = [];
            hidden = [.. _hidden];
            _hidden.Clear();
            pid = _tposdPid;
        }

        try { cts?.Cancel(); } catch { }
        // Show only windows hidden by this suppressor, and only if the OEM
        // still owns the HWND. Never blindly show unrelated OS windows.
        if (pid is int expectedPid)
        {
            foreach (IntPtr hwnd in hidden)
            {
                if (_stillOwned(hwnd, expectedPid)) _setVisible(hwnd, true);
            }
        }
        cts?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Disarm();
        _disposed = true;
    }

    private const int SwHide = 0;
    private const int SwShowNoActivate = 8;

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int command);
}
