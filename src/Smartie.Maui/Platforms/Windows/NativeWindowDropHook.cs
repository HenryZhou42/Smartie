using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Smartie.Shared.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation;
using WinDragEventArgs = Microsoft.UI.Xaml.DragEventArgs;
using WinUIElement = Microsoft.UI.Xaml.UIElement;

namespace Smartie.Maui.Platform;

/// <summary>
/// Handles Explorer drag-and-drop on the WinUI window root. MAUI
/// <see cref="Microsoft.Maui.Controls.DropGestureRecognizer"/> on BlazorWebView does not
/// reliably keep Smartie in the foreground after a drop.
/// </summary>
internal static class NativeWindowDropHook
{
    private static readonly HashSet<IntPtr> HookedRoots = [];

    public static void Install(MauiWinUIWindow window)
    {
        void TryHook()
        {
            if (window.Content is not WinUIElement root)
            {
                return;
            }

            var key = WinRT.Interop.WindowNative.GetWindowHandle(window);
            if (!HookedRoots.Add(key))
            {
                return;
            }

            root.AllowDrop = true;
            root.DragOver -= OnDragOver;
            root.DragLeave -= OnDragLeave;
            root.Drop -= OnDrop;
            root.DragOver += OnDragOver;
            root.DragLeave += OnDragLeave;
            root.Drop += OnDrop;
        }

        TryHook();

        if (window.Content is null)
        {
            window.Activated += OnWindowActivated;

            void OnWindowActivated(object sender, WindowActivatedEventArgs args)
            {
                TryHook();
                if (window.Content is not null)
                {
                    window.Activated -= OnWindowActivated;
                }
            }
        }
    }

    private static INativeKnowledgeDropBridge? GetBridge() =>
        IPlatformApplication.Current?.Services.GetService<INativeKnowledgeDropBridge>();

    private static void OnDragOver(object sender, WinDragEventArgs e)
    {
        var bridge = GetBridge();
        if (bridge?.IsAcceptingDrops != true)
        {
            return;
        }

        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        e.AcceptedOperation = WinOperation.Copy;
        e.Handled = true;
        bridge.NotifyDragState(true);
    }

    private static void OnDragLeave(object sender, WinDragEventArgs e)
    {
        GetBridge()?.NotifyDragState(false);
    }

    private static async void OnDrop(object sender, WinDragEventArgs e)
    {
        var bridge = GetBridge();
        bridge?.NotifyDragState(false);

        if (bridge?.IsAcceptingDrops != true)
        {
            return;
        }

        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        e.Handled = true;
        RestoreWindowAndWebViewFocus();

        IReadOnlyList<string> paths;
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            paths = items
                .OfType<StorageFile>()
                .Select(file => file.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();
        }
        catch
        {
            ScheduleFocusRestore();
            return;
        }

        RestoreWindowAndWebViewFocus();

        if (paths.Count == 0)
        {
            ScheduleFocusRestore();
            return;
        }

        _ = ProcessDropAsync(bridge, paths);
        ScheduleFocusRestore();
    }

    private static async Task ProcessDropAsync(INativeKnowledgeDropBridge bridge, IReadOnlyList<string> paths)
    {
        try
        {
            await bridge.RaiseFilesDroppedAsync(paths).ConfigureAwait(true);
        }
        catch
        {
            // Knowledge page shows upload errors from its own handler.
        }
        finally
        {
            ScheduleFocusRestore();
        }
    }

    private static void ScheduleFocusRestore()
    {
        RestoreWindowAndWebViewFocus();

        var dispatcher = Microsoft.Maui.Controls.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), RestoreWindowAndWebViewFocus);
        dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), RestoreWindowAndWebViewFocus);
        dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(400), RestoreWindowAndWebViewFocus);
    }

    private static void RestoreWindowAndWebViewFocus()
    {
        WindowsAppFocus.RestoreToForeground();

        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is MauiWinUIWindow mauiWindow &&
            FindWebView2(mauiWindow.Content) is WebView2 webView2)
        {
            webView2.Focus(FocusState.Programmatic);
        }
    }

    private static WebView2? FindWebView2(object? element)
    {
        if (element is WebView2 webView2)
        {
            return webView2;
        }

        if (element is not DependencyObject dependencyObject)
        {
            return null;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(dependencyObject);
        for (var i = 0; i < childCount; i++)
        {
            var found = FindWebView2(VisualTreeHelper.GetChild(dependencyObject, i));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
