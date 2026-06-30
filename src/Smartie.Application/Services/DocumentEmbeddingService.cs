using Microsoft.Extensions.Logging;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public sealed class DocumentEmbeddingService : IDocumentEmbeddingService
{
    private readonly IDocumentRepository _documents;
    private readonly IDocumentChunkRepository _chunks;
    private readonly IAiSettingsService _aiSettings;
    private readonly IEmbeddingProviderFactory _embeddingFactory;
    private readonly ILogger<DocumentEmbeddingService> _logger;

    public DocumentEmbeddingService(
        IDocumentRepository documents,
        IDocumentChunkRepository chunks,
        IAiSettingsService aiSettings,
        IEmbeddingProviderFactory embeddingFactory,
        ILogger<DocumentEmbeddingService> logger)
    {
        _documents = documents;
        _chunks = chunks;
        _aiSettings = aiSettings;
        _embeddingFactory = embeddingFactory;
        _logger = logger;
    }

    public Task<Document> GenerateAndPersistAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        EmbedInternalAsync(documentId, userId, rebuild: false, cancellationToken);

    public Task<Document> RebuildEmbeddingsAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        EmbedInternalAsync(documentId, userId, rebuild: true, cancellationToken);

    private async Task<Document> EmbedInternalAsync(
        Guid documentId,
        Guid userId,
        bool rebuild,
        CancellationToken cancellationToken)
    {
        var document = await _documents
            .FindForUpdateAsync(documentId, userId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Document {documentId} was not found.");

        if (!document.IsChunked || document.ChunkCount <= 0)
        {
            throw new InvalidOperationException("Document must be chunked before generating embeddings.");
        }

        var chunkList = await _chunks
            .GetTrackedForDocumentAsync(documentId, cancellationToken)
            .ConfigureAwait(false);

        if (chunkList.Count == 0)
        {
            throw new InvalidOperationException("No chunks found for this document.");
        }

        if (!rebuild)
        {
            var pending = chunkList.Count(c =>
                c.EmbeddingStatus is ChunkEmbeddingStatus.Pending or ChunkEmbeddingStatus.Failed);
            if (pending == 0)
            {
                return document;
            }
        }
        else
        {
            foreach (var chunk in chunkList)
            {
                chunk.EmbeddingVector = null;
                chunk.EmbeddingModel = null;
                chunk.EmbeddingGeneratedAt = null;
                chunk.EmbeddingStatus = ChunkEmbeddingStatus.Pending;
            }
        }

        IEmbeddingProvider provider;
        try
        {
            var settings = await _aiSettings.ResolveEmbeddingAsync(userId, cancellationToken).ConfigureAwait(false);
            provider = _embeddingFactory.Create(settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve embedding provider for document {DocumentId}.", documentId);
            foreach (var chunk in chunkList.Where(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Pending))
            {
                chunk.EmbeddingStatus = ChunkEmbeddingStatus.Failed;
            }

            UpdateDocumentEmbeddingState(document, chunkList, providerModel: null);
            await _documents.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        foreach (var chunk in chunkList)
        {
            if (!rebuild &&
                chunk.EmbeddingStatus is not (ChunkEmbeddingStatus.Pending or ChunkEmbeddingStatus.Failed))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            chunk.EmbeddingStatus = ChunkEmbeddingStatus.Generating;
            await _documents.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var vector = await provider
                    .GenerateEmbeddingAsync(chunk.Content, cancellationToken)
                    .ConfigureAwait(false);

                chunk.EmbeddingVector = EmbeddingVectorConverter.ToBytes(vector);
                chunk.EmbeddingModel = provider.ModelName;
                chunk.EmbeddingGeneratedAt = DateTimeOffset.UtcNow;
                chunk.EmbeddingStatus = ChunkEmbeddingStatus.Completed;
            }
            catch (Exception ex)
            {
                chunk.EmbeddingStatus = ChunkEmbeddingStatus.Failed;
                _logger.LogWarning(
                    ex,
                    "Embedding generation failed for chunk {ChunkIndex} of document {DocumentId}.",
                    chunk.ChunkIndex,
                    documentId);
            }
        }

        UpdateDocumentEmbeddingState(document, chunkList, provider.ModelName);
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await _documents.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Embedded {EmbeddedCount}/{ChunkCount} chunks for document {DocumentId} using {Model}.",
            document.EmbeddedChunkCount,
            document.ChunkCount,
            documentId,
            provider.ModelName);

        return document;
    }

    private static void UpdateDocumentEmbeddingState(
        Document document,
        IReadOnlyList<DocumentChunk> chunks,
        string? providerModel)
    {
        var completed = chunks.Count(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Completed);
        document.EmbeddedChunkCount = completed;
        document.IsEmbedded = completed > 0 && completed == chunks.Count;
        document.EmbeddingModel = completed > 0 ? providerModel : null;
        document.EmbeddedAt = completed > 0 ? DateTimeOffset.UtcNow : null;
    }
}
