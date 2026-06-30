namespace Smartie.Application.Abstractions;

public interface IAppMetricsService
{
    void RecordStartup(TimeSpan duration);

    void RecordSearchLatency(long milliseconds);

    void RecordRagLatency(long milliseconds);

    PerformanceMetricsSnapshot GetMetrics();
}

public sealed record PerformanceMetricsSnapshot(
    long StartupTimeMs,
    long LastSearchLatencyMs,
    long LastRagLatencyMs);
