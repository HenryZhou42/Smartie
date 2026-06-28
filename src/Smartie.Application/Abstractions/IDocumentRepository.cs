using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IDocumentRepository
{
    Task<IReadOnlyList<Document>> ListAsync(Guid userId, string? search, CancellationToken cancellationToken = default);

    Task<Document?> FindAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);

    Task<DocumentStats> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Document> AddAsync(Document document, CancellationToken cancellationToken = default);

    Task<Document?> UpdateNameAsync(Guid documentId, Guid userId, string name, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);

    Task<Document?> FindForUpdateAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record DocumentStats(
    int DocumentCount,
    int IndexedCount,
    long TotalSizeBytes,
    int RecentUploadCount,
    int ExtractedCount,
    long TotalExtractedCharacters,
    DateTimeOffset? LastExtractedAt,
    string? LastExtractorUsed);
