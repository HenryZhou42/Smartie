using Smartie.Application.Abstractions;

namespace Smartie.Application.Services;

public sealed class OnboardingService : IOnboardingService
{
    private static readonly string[] SampleFileNames =
    [
        "Smartie_Test_Document.md",
        "CompanyPolicy.md",
        "ASPNetNotes.md"
    ];

    private readonly IOnboardingRepository _onboarding;
    private readonly IAiSettingsService _aiSettings;
    private readonly IDocumentRepository _documents;
    private readonly IDocumentService _documentService;

    public OnboardingService(
        IOnboardingRepository onboarding,
        IAiSettingsService aiSettings,
        IDocumentRepository documents,
        IDocumentService documentService)
    {
        _onboarding = onboarding;
        _aiSettings = aiSettings;
        _documents = documents;
        _documentService = documentService;
    }

    public async Task<OnboardingStatusSnapshot> GetStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var completed = await _onboarding.GetHasCompletedOnboardingAsync(userId, cancellationToken).ConfigureAwait(false);
        var snapshot = await _aiSettings.GetSnapshotAsync(userId, cancellationToken).ConfigureAwait(false);
        var active = snapshot.Providers.FirstOrDefault(p =>
            string.Equals(p.Info.Key, snapshot.SelectedProvider, StringComparison.OrdinalIgnoreCase));
        var hasProvider = active is not null &&
            active.Info.Available &&
            (!active.Info.RequiresApiKey || active.HasApiKey);

        var stats = await _documents.GetStatsAsync(userId, cancellationToken).ConfigureAwait(false);
        return new OnboardingStatusSnapshot(
            completed,
            hasProvider,
            stats.DocumentCount,
            FindSampleFolder() is not null);
    }

    public Task CompleteAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _onboarding.SetCompletedOnboardingAsync(userId, cancellationToken);

    public async Task<SampleImportResult> ImportSampleDocumentsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var folder = FindSampleFolder()
            ?? throw new InvalidOperationException("Sample documents folder was not found.");

        var existing = await _documents.ListAsync(userId, null, cancellationToken).ConfigureAwait(false);
        var existingNames = existing.Select(d => d.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var imported = new List<string>();
        var skipped = 0;

        foreach (var fileName in SampleFileNames)
        {
            var path = Path.Combine(folder, fileName);
            if (!File.Exists(path))
            {
                skipped++;
                continue;
            }

            if (existingNames.Contains(fileName))
            {
                skipped++;
                continue;
            }

            await using var stream = File.OpenRead(path);
            var info = new FileInfo(path);
            var document = await _documentService.UploadAsync(userId, fileName, stream, info.Length, cancellationToken)
                .ConfigureAwait(false);
            await _onboarding.MarkDocumentAsSampleAsync(document.Id, userId, cancellationToken).ConfigureAwait(false);
            imported.Add(document.Name);
        }

        return new SampleImportResult(imported.Count, skipped, imported);
    }

    internal static string? FindSampleFolder()
    {
        var names = new[] { "SampleData", "TestData" };
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            foreach (var name in names)
            {
                var candidate = Path.Combine(current, name);
                if (Directory.Exists(candidate) &&
                    SampleFileNames.Any(f => File.Exists(Path.Combine(candidate, f))))
                {
                    return candidate;
                }
            }

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        return null;
    }
}
