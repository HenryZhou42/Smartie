using System.Runtime.InteropServices;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace Smartie.Maui.Platform;

/// <summary>
/// Ensures unpackaged WinUI windows use the Smartie .ico (taskbar + title bar).
/// MSIX packaged builds also pick up MauiIcon-generated tiles via Package.appxmanifest.
/// </summary>
internal static class WindowIconHelper
{
    private const int IconSmall = 0;
    private const int IconBig = 1;
    private const uint WmSetIcon = 0x0080;
    private const uint ImageIcon = 1;
    private const uint LrDefaultSize = 0x00000040;
    private const uint LrLoadFromFile = 0x00000010;
    private const uint LrShared = 0x00008000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadImage(nint hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    public static void Configure(ILifecycleBuilder events)
    {
        events.AddWindows(windows =>
        {
            windows.OnWindowCreated(window =>
            {
                try
                {
                    Apply(window);
                }
                catch
                {
                    // Icon is best-effort; never block app startup.
                }
            });
        });
    }

    private static void Apply(Microsoft.UI.Xaml.Window window)
    {
        var icoPath = ResolveIconPath();
        if (icoPath is null)
        {
            return;
        }

        var hwnd = WindowNative.GetWindowHandle(window);
        if (hwnd == 0)
        {
            return;
        }

        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow?.SetIcon(icoPath);

        // Also push classic WM_SETICON so Explorer/taskbar refresh reliably for unpackaged EXE.
        var small = LoadImage(0, icoPath, ImageIcon, 16, 16, LrLoadFromFile | LrDefaultSize | LrShared);
        var big = LoadImage(0, icoPath, ImageIcon, 32, 32, LrLoadFromFile | LrDefaultSize | LrShared);
        if (small != 0)
        {
            SendMessage(hwnd, WmSetIcon, IconSmall, small);
        }

        if (big != 0)
        {
            SendMessage(hwnd, WmSetIcon, IconBig, big);
        }
    }

    private static string? ResolveIconPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "smartie.ico"),
            Path.Combine(AppContext.BaseDirectory, "Resources", "AppIcon", "smartie.ico"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
