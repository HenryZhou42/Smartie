using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Smartie.Application.Abstractions;
using Smartie.Infrastructure.Persistence;

namespace Smartie.Infrastructure.Automation;

public sealed class AutomationSchedulerHostedService : IHostedService, IDisposable
{
    private readonly IServiceProvider _services;
    private Timer? _timer;

    public AutomationSchedulerHostedService(IServiceProvider services)
    {
        _services = services;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var automations = scope.ServiceProvider.GetRequiredService<IAutomationService>();
        await automations.SeedExamplesAsync(DbInitializer.LocalUserId, cancellationToken).ConfigureAwait(false);
        await automations.ProcessDueScheduledAsync(cancellationToken).ConfigureAwait(false);

        _timer = new Timer(
            _ => _ = ProcessDueAsync(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();

    private async Task ProcessDueAsync()
    {
        try
        {
            using var scope = _services.CreateScope();
            var automations = scope.ServiceProvider.GetRequiredService<IAutomationService>();
            await automations.ProcessDueScheduledAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Scheduler failures should not crash the host.
        }
    }
}
