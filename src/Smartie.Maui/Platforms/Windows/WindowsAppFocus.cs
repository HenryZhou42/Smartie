using System.Runtime.InteropServices;
using Microsoft.Maui.Platform;

namespace Smartie.Maui.Platform;

/// <summary>
/// WinUI <see cref="Microsoft.UI.Xaml.Window.Activate"/> often fails to reclaim foreground
/// after an external drag-and-drop. Use Win32 focus APIs during the drop event itself.
/// </summary>
internal static class WindowsAppFocus
{
    private static readonly IntPtr HwndTop = new(0);
    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNotTopMost = new(-2);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AttachThreadInput(uint attachThreadId, uint attachToThreadId, bool attach);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetActiveWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    private const uint SwpNomove = 0x0002;
    private const uint SwpNosize = 0x0001;
    private const uint SwpShowwindow = 0x0040;

    public static void RestoreToForeground()
    {
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is not MauiWinUIWindow mauiWindow)
        {
            return;
        }

        var targetHwnd = mauiWindow.WindowHandle;
        if (targetHwnd == IntPtr.Zero)
        {
            mauiWindow.Activate();
            return;
        }

        try
        {
            var foregroundHwnd = GetForegroundWindow();
            if (foregroundHwnd == targetHwnd)
            {
                mauiWindow.Activate();
                return;
            }

            var foregroundThread = GetWindowThreadProcessId(foregroundHwnd, IntPtr.Zero);
            var currentThread = GetCurrentThreadId();
            var attached = false;

            if (foregroundThread != 0 && foregroundThread != currentThread)
            {
                attached = AttachThreadInput(currentThread, foregroundThread, true);
            }

            try
            {
                SetWindowPos(
                    targetHwnd,
                    HwndTop,
                    0,
                    0,
                    0,
                    0,
                    SwpNomove | SwpNosize | SwpShowwindow);
                BringWindowToTop(targetHwnd);
                SetWindowPos(
                    targetHwnd,
                    HwndTopMost,
                    0,
                    0,
                    0,
                    0,
                    SwpNomove | SwpNosize);
                SetWindowPos(
                    targetHwnd,
                    HwndNotTopMost,
                    0,
                    0,
                    0,
                    0,
                    SwpNomove | SwpNosize);
                SetForegroundWindow(targetHwnd);
                SetActiveWindow(targetHwnd);
                SetFocus(targetHwnd);
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(currentThread, foregroundThread, false);
                }
            }
        }
        catch (Exception)
        {
            // Never block drag-and-drop if Win32 focus APIs fail.
        }

        mauiWindow.Activate();
    }
}
