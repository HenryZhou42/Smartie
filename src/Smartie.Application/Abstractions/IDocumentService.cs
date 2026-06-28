using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IDocumentService
{
    Task<IReadOnlyList<Document>> ListAsync(Guid userId, string? search, CancellationToken cancellationToken = default);

    Task<DocumentStats> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Document?> GetAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default);

    Task<Document> UploadAsync(
        Guid userId,
        string originalFileName,
        Stream content,
        long sizeBytes,
        CancellationToken cancellationToken = default);

    Task<Document?> RenameAsync(
        Guid userId,
        Guid documentId,
        string newName,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default);

    Task<(Document Document, string AbsolutePath)?> GetForOpenAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    KnowledgeBaseSettingsSnapshot GetSettings();
}

public sealed record KnowledgeBaseSettingsSnapshot(
    string StorageFolder,
    long MaxFileSizeBytes,
    string? DefaultCollection,
    IReadOnlyList<string> SupportedExtensions,
    IReadOnlyList<string> FutureExtensions);
