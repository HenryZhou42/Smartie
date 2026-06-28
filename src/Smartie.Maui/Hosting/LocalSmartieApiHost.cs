using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Smartie.Api;

namespace Smartie.Maui.Hosting;

/// <summary>
/// Runs the Smartie API in-process on loopback so the desktop app is a single launch target.
/// </summary>
public sealed class LocalSmartieApiHost : IAsyncDisposable
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(2);

    private readonly IConfiguration _configuration;
    private readonly ILogger<LocalSmartieApiHost> _logger;
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private WebApplication? _app;
    private int _stopStarted;

    public LocalSmartieApiHost(IConfiguration configuration, ILogger<LocalSmartieApiHost> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string BaseUrl { get; private set; } = string.Empty;

    public bool IsRunning => _app is not null;

    /// <summary>
    /// Starts the API on first use. Safe to call from multiple threads.
    /// </summary>
    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return;
        }

        await _startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_app is not null)
            {
                return;
            }

            await StartAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _startGate.Release();
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return;
        }

        Interlocked.Exchange(ref _stopStarted, 0);

        var preferredPort = _configuration.GetValue("SmartieApi:Port", 5220);
        var urls = new[] { $"http://127.0.0.1:{preferredPort}", "http://127.0.0.1:0" };

        Exception? lastError = null;
        foreach (var url in urls)
        {
            try
            {
                await StartOnUrlAsync(url, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Smartie local API listening on {BaseUrl}", BaseUrl);
                return;
            }
            catch (Exception ex) when (url != urls[^1])
            {
                lastError = ex;
                _logger.LogWarning(ex, "Could not bind Smartie API to {Url}, trying next option.", url);
                await StopInternalAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Smartie local API could not start.", lastError);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _stopStarted, 1) != 0)
        {
            return;
        }

        await StopInternalAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Smartie local API stopped.");
    }

    /// <summary>
    /// Stops the API without blocking the UI thread. Safe to call from window lifecycle events.
    /// </summary>
    public void StopInBackground()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(ShutdownTimeout);
                await StopAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Smartie local API background stop timed out.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Smartie local API background stop failed.");
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _startGate.Dispose();
    }

    private async Task StartOnUrlAsync(string url, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(LocalSmartieApiHost).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Configuration.AddConfiguration(_configuration);
        builder.WebHost.UseUrls(url);
        builder.Services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = ShutdownTimeout);

        builder.AddSmartieApi();

        var app = builder.Build();
        await app.InitializeDatabaseAsync(cancellationToken).ConfigureAwait(false);
        app.MapSmartieApi();

        await app.StartAsync(cancellationToken).ConfigureAwait(false);

        _app = app;
        BaseUrl = ResolveListeningUrl(app, url);
    }

    private async Task StopInternalAsync(CancellationToken cancellationToken)
    {
        var app = Interlocked.Exchange(ref _app, null);
        if (app is null)
        {
            BaseUrl = string.Empty;
            return;
        }

        BaseUrl = string.Empty;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ShutdownTimeout);
            await app.StopAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Smartie local API graceful shutdown timed out.");
        }
        finally
        {
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string ResolveListeningUrl(WebApplication app, string requestedUrl)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses is { Count: > 0 })
        {
            return addresses.First().TrimEnd('/');
        }

        return requestedUrl.TrimEnd('/');
    }
}
