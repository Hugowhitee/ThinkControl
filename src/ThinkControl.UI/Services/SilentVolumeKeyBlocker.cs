using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ThinkControl.UI.Services;

/// <summary>
/// Session-only guard used by Audio Safety Silent. Windows volume keys are
/// swallowed before they reach the shell while Silent owns the output mute.
/// CoreAudio notifications remain the second line of defense for app/SndVol
/// changes and for any keyboard path that does not arrive as a standard
/// VK_VOLUME_* key.
/// </summary>
internal sealed class SilentVolumeKeyBlocker : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const uint WmKeyDown = 0x0100;
    private const uint WmSysKeyDown = 0x0104;
    private const uint VkVolumeMute = 0xAD;
    private const uint VkVolumeDown = 0xAE;
    private const uint VkVolumeUp = 0xAF;

    private readonly HookProc _callback;
    private IntPtr _hook;
    private bool _disposed;

    internal SilentVolumeKeyBlocker()
    {
        _callback = HookCallback;
        using Process process = Process.GetCurrentProcess();
        using ProcessModule? module = process.MainModule;
        IntPtr moduleHandle = module is null ? IntPtr.Zero : GetModuleHandle(module.ModuleName);
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, moduleHandle, 0);
    }

    internal bool IsAvailable => _hook != IntPtr.Zero;

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            uint vkCode = unchecked((uint)Marshal.ReadInt32(lParam));
            uint message = unchecked((uint)wParam.ToInt64());
            bool volumeKey = vkCode is VkVolumeMute or VkVolumeDown or VkVolumeUp;
            bool keyDown = message is WmKeyDown or WmSysKeyDown;

            if (volumeKey && keyDown)
            {
                // Suppress only key-down/repeat events. Key-up is deliberately allowed
                // through. If Silent is enabled while the user is already holding a
                // volume key, swallowing the release event can leave Windows behaving
                // as if that pre-hook key press is still held. Blocking future key-down
                // repeats is enough to stop new volume changes while preserving a clean
                // release for an activation-race key that started before the hook.
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
