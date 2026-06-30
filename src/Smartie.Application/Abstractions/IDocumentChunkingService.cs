using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IDocumentChunkingService
{
    Task<Document> ChunkAndPersistAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Document> RebuildChunksAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
