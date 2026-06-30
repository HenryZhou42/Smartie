namespace Smartie.Application.Abstractions;

public interface IOnboardingService
{
    Task<OnboardingStatusSnapshot> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    Task CompleteAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<SampleImportResult> ImportSampleDocumentsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record OnboardingStatusSnapshot(
    bool HasCompletedOnboarding,
    bool HasConfiguredProvider,
    int DocumentCount,
    bool SampleDocumentsAvailable);

public sealed record SampleImportResult(
    int ImportedCount,
    int SkippedCount,
    IReadOnlyList<string> ImportedNames);
