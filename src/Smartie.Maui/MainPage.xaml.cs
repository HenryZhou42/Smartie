using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.Logging;
using Smartie.Maui.Hosting;

#if WINDOWS
using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml.Controls;
using Smartie.Maui.Platform;
#endif

namespace Smartie.Maui;

public partial class MainPage : ContentPage
{
    private bool _webViewInitialized;
    private bool _apiWarmupStarted;

    public MainPage()
    {
        WebAssetsBootstrapper.EnsureHostPagePresent();
        InitializeComponent();

        blazorWebView.BlazorWebViewInitializing += OnBlazorWebViewInitializing;
        blazorWebView.BlazorWebViewInitialized += OnBlazorWebViewInitialized;
#if WINDOWS
        blazorWebView.HandlerChanged += OnBlazorWebViewHandlerChanged;
#endif

        WriteStartupLog($"BaseDirectory: {AppContext.BaseDirectory}");
        WriteStartupLog($"HostPage exists: {File.Exists(WebAssetsBootstrapper.ResolveHostPagePath())}");

        _ = MonitorStartupAsync();
    }

    private void OnBlazorWebViewInitializing(object? sender, BlazorWebViewInitializingEventArgs e)
    {
        e.UserDataFolder = WebView2Bootstrapper.ResolveUserDataFolder();

        var browserFolder = WebView2Bootstrapper.ResolveBrowserExecutableFolder();
        if (!string.IsNullOrWhiteSpace(browserFolder))
        {
            e.BrowserExecutableFolder = browserFolder;
        }

        WriteStartupLog(
            $"BlazorWebView initializing. UserDataFolder={e.UserDataFolder}, BrowserExecutableFolder={browserFolder ?? "(default)"}");
    }

#if WINDOWS
    private void OnBlazorWebViewHandlerChanged(object? sender, EventArgs e)
    {
        if (blazorWebView.Handler?.PlatformView is not WebView2 webView2)
        {
            return;
        }

        webView2.CoreWebView2Initialized += (_, _) =>
        {
            WriteStartupLog("CoreWebView2 initialized on platform WebView2 control.");
        };
    }
#endif

    private void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
    {
        _webViewInitialized = true;
        WriteStartupLog("BlazorWebView initialized.");

#if WINDOWS
        if (Window?.Handler?.PlatformView is MauiWinUIWindow mauiWindow)
        {
            NativeWindowDropHook.Install(mauiWindow);
        }
#endif

        MainThread.BeginInvokeOnMainThread(() =>
        {
            startupOverlay.IsVisible = false;
        });

        StartEmbeddedApiWarmup();
    }

    private void StartEmbeddedApiWarmup()
    {
        if (_apiWarmupStarted)
        {
            return;
        }

        _apiWarmupStarted = true;
        _ = Task.Run(WarmUpEmbeddedApiAsync);
    }

    private async Task MonitorStartupAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(12)).ConfigureAwait(false);

        if (_webViewInitialized)
        {
            return;
        }

        var hostPagePath = WebAssetsBootstrapper.ResolveHostPagePath();
        var message = File.Exists(hostPagePath)
            ? "WebView2 did not finish loading. Try: close Smartie, delete %LOCALAPPDATA%\\Smartie\\WebView2, rebuild, and run again."
            : $"Web UI assets missing at {hostPagePath}. Rebuild the solution and try again.";

        WriteStartupLog(message);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            startupStatusLabel.Text = message;
        });
    }

    private static async Task WarmUpEmbeddedApiAsync()
    {
        var services = IPlatformApplication.Current?.Services;
        if (services?.GetService<LocalSmartieApiHost>() is not { } apiHost)
        {
            return;
        }

        try
        {
            await apiHost.EnsureStartedAsync().ConfigureAwait(false);
            WriteStartupLog($"Embedded API listening on {apiHost.BaseUrl}");
        }
        catch (Exception ex)
        {
            WriteStartupLog($"Embedded API warmup failed: {ex.Message}");
            services.GetService<ILogger<MainPage>>()?.LogWarning(ex, "Embedded Smartie API warmup failed.");
        }
    }

    private static void WriteStartupLog(string message)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Smartie",
                "Logs");
            Directory.CreateDirectory(logDir);
            var line = $"{DateTimeOffset.Now:u} {message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(logDir, "startup.log"), line);
        }
        catch
        {
            // Best-effort diagnostics only.
        }

        System.Diagnostics.Debug.WriteLine(message);
    }
}
