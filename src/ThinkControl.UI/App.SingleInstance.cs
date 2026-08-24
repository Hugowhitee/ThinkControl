using System.Threading;
using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    private const string SingleInstanceMutexName = @"Local\ThinkControl.SingleInstance.v1";
    private const string SingleInstanceActivateName = @"Local\ThinkControl.Activate.v1";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _singleInstanceActivate;
    private CancellationTokenSource? _singleInstanceCts;
    private bool _ownsSingleInstanceMutex;

    private void InitializeSingleInstanceGuard()
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
        _ownsSingleInstanceMutex = createdNew;

        if (!createdNew)
        {
            try
            {
                using EventWaitHandle activation = EventWaitHandle.OpenExisting(SingleInstanceActivateName);
                activation.Set();
            }
            catch
            {
            }

            // A second desktop/start-menu launch activates the existing process and
            // exits before a second NotifyIcon can ever be created.
            Environment.Exit(0);
            return;
        }

        _singleInstanceActivate = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            SingleInstanceActivateName);
        _singleInstanceCts = new CancellationTokenSource();
        CancellationToken token = _singleInstanceCts.Token;

        _ = Task.Run(() =>
        {
            WaitHandle[] handles = [_singleInstanceActivate, token.WaitHandle];
            while (!token.IsCancellationRequested)
            {
                int signaled;
                try { signaled = WaitHandle.WaitAny(handles); }
                catch { return; }
                if (signaled != 0 || token.IsCancellationRequested)
                    return;

                try
                {
                    Dispatcher.BeginInvoke(
                        DispatcherPriority.ApplicationIdle,
                        new Action(ShowThinkControlFromDesktopLaunch));
                }
                catch
                {
                    return;
                }
            }
        }, token);

        Exit += (_, _) => DisposeSingleInstanceGuard();
    }

    private void ShowThinkControlFromDesktopLaunch()
    {
        // Re-opening ThinkControl should behave like a fresh user launch and honor
        // the saved Compact / Advanced preference instead of always forcing Advanced.
        ShowPreferredDesktopLaunchView();
    }

    private void DisposeSingleInstanceGuard()
    {
        try { _singleInstanceCts?.Cancel(); } catch { }
        try { _singleInstanceActivate?.Set(); } catch { }
        try { _singleInstanceActivate?.Dispose(); } catch { }
        _singleInstanceActivate = null;
        try { _singleInstanceCts?.Dispose(); } catch { }
        _singleInstanceCts = null;

        if (_ownsSingleInstanceMutex)
        {
            try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
        }
        try { _singleInstanceMutex?.Dispose(); } catch { }
        _singleInstanceMutex = null;
        _ownsSingleInstanceMutex = false;
    }
}
