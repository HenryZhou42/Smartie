namespace Smartie.Contracts;

public sealed record OnboardingStatusDto(
    bool HasCompletedOnboarding,
    bool HasConfiguredProvider,
    int DocumentCount,
    bool SampleDocumentsAvailable);

public sealed record SampleImportResultDto(
    int ImportedCount,
    int SkippedCount,
    IReadOnlyList<string> ImportedNames);

public sealed record AppInfoDto(
    string ProductName,
    string Edition,
    string Version,
    string BuildNumber,
    string BuildDate,
    string ReleaseLabel,
    string Description,
    string GitHubUrl,
    string License);

public sealed record PerformanceMetricsDto(
    long StartupTimeMs,
    long LastSearchLatencyMs,
    long LastRagLatencyMs);
