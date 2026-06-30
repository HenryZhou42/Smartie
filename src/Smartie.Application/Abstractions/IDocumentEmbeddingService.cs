using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IDocumentEmbeddingService
{
    Task<Document> GenerateAndPersistAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Document> RebuildEmbeddingsAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
