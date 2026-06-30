using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Smartie.Application.Abstractions;

namespace Smartie.Infrastructure.Startup;

public sealed class AppStartupMetricsHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public AppStartupMetricsHostedService(IServiceProvider services)
    {
        _services = services;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var metrics = _services.GetRequiredService<IAppMetricsService>();
        metrics.RecordStartup(DateTimeOffset.UtcNow - _startedAt);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
