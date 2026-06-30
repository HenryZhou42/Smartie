using Microsoft.EntityFrameworkCore;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Persistence;

public sealed class DocumentChunkRepository : IDocumentChunkRepository
{
    private readonly SmartieDbContext _db;

    public DocumentChunkRepository(SmartieDbContext db)
    {
        _db = db;
    }

    public async Task ReplaceForDocumentAsync(
        Guid documentId,
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing.Count > 0)
        {
            _db.DocumentChunks.RemoveRange(existing);
        }

        foreach (var chunk in chunks)
        {
            chunk.DocumentId = documentId;
            _db.DocumentChunks.Add(chunk);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing.Count > 0)
        {
            _db.DocumentChunks.RemoveRange(existing);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        await _db.DocumentChunks
            .AsNoTracking()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> GetCountForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        await _db.DocumentChunks
            .AsNoTracking()
            .CountAsync(c => c.DocumentId == documentId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<double> GetAverageLengthForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var average = await _db.DocumentChunks
            .AsNoTracking()
            .Where(c => c.DocumentId == documentId)
            .AverageAsync(c => (double?)c.CharacterCount, cancellationToken)
            .ConfigureAwait(false);

        return average ?? 0;
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetTrackedForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        await _db.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<ChunkEmbeddingStats> GetEmbeddingStatsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var chunks = await (
                from chunk in _db.DocumentChunks.AsNoTracking()
                join document in _db.Documents.AsNoTracking() on chunk.DocumentId equals document.Id
                where document.UserId == userId
                select new { chunk.EmbeddingStatus, chunk.EmbeddingVector })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var completed = chunks.Count(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Completed);
        var failed = chunks.Count(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Failed);
        int? dimension = null;

        var sample = chunks.FirstOrDefault(c => c.EmbeddingVector is { Length: > 0 })?.EmbeddingVector;
        if (sample is not null)
        {
            dimension = EmbeddingVectorConverter.FromBytes(sample).Length;
        }

        return new ChunkEmbeddingStats(completed, failed, dimension);
    }

    public async Task<IReadOnlyList<SearchableChunkRow>> GetSearchableChunksForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await (
                from chunk in _db.DocumentChunks.AsNoTracking()
                join document in _db.Documents.AsNoTracking() on chunk.DocumentId equals document.Id
                where document.UserId == userId
                      && chunk.EmbeddingStatus == ChunkEmbeddingStatus.Completed
                      && chunk.EmbeddingVector != null
                orderby document.Name, chunk.ChunkIndex
                select new SearchableChunkRow(
                    chunk.Id,
                    chunk.DocumentId,
                    document.Name,
                    document.FileName,
                    chunk.ChunkIndex,
                    chunk.Content,
                    chunk.PageNumber,
                    chunk.EmbeddingVector!))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
