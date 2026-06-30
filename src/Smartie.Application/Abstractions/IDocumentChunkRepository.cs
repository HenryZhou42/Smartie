using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IDocumentChunkRepository
{
    Task ReplaceForDocumentAsync(
        Guid documentId,
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);

    Task DeleteForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> GetForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<int> GetCountForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<double> GetAverageLengthForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> GetTrackedForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<ChunkEmbeddingStats> GetEmbeddingStatsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchableChunkRow>> GetSearchableChunksForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record ChunkEmbeddingStats(
    int CompletedCount,
    int FailedCount,
    int? VectorDimension);
