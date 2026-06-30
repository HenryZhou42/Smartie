using Smartie.Application.Abstractions;

namespace Smartie.Application.Services;

public sealed class AppMetricsService : IAppMetricsService
{
    private long _startupTimeMs;
    private long _lastSearchLatencyMs;
    private long _lastRagLatencyMs;

    public void RecordStartup(TimeSpan duration) =>
        Interlocked.Exchange(ref _startupTimeMs, (long)duration.TotalMilliseconds);

    public void RecordSearchLatency(long milliseconds) =>
        Interlocked.Exchange(ref _lastSearchLatencyMs, milliseconds);

    public void RecordRagLatency(long milliseconds) =>
        Interlocked.Exchange(ref _lastRagLatencyMs, milliseconds);

    public PerformanceMetricsSnapshot GetMetrics() =>
        new(_startupTimeMs, _lastSearchLatencyMs, _lastRagLatencyMs);
}
