using Microsoft.Extensions.Logging;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public sealed class DocumentChunkingService : IDocumentChunkingService
{
    private readonly IDocumentRepository _documents;
    private readonly IDocumentChunkRepository _chunks;
    private readonly IDocumentChunker _chunker;
    private readonly IDocumentEmbeddingService _embedding;
    private readonly ILogger<DocumentChunkingService> _logger;

    public DocumentChunkingService(
        IDocumentRepository documents,
        IDocumentChunkRepository chunks,
        IDocumentChunker chunker,
        IDocumentEmbeddingService embedding,
        ILogger<DocumentChunkingService> logger)
    {
        _documents = documents;
        _chunks = chunks;
        _chunker = chunker;
        _embedding = embedding;
        _logger = logger;
    }

    public Task<Document> ChunkAndPersistAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        ChunkInternalAsync(documentId, userId, skipIfAlreadyChunked: true, cancellationToken);

    public Task<Document> RebuildChunksAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        ChunkInternalAsync(documentId, userId, skipIfAlreadyChunked: false, cancellationToken);

    private async Task<Document> ChunkInternalAsync(
        Guid documentId,
        Guid userId,
        bool skipIfAlreadyChunked,
        CancellationToken cancellationToken)
    {
        var document = await _documents
            .FindForUpdateAsync(documentId, userId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Document {documentId} was not found.");

        if (document.ExtractionStatus != DocumentExtractionStatus.Completed ||
            string.IsNullOrWhiteSpace(document.ExtractedText))
        {
            throw new InvalidOperationException("Document text must be extracted before chunking.");
        }

        if (skipIfAlreadyChunked && document.IsChunked && document.ChunkCount > 0)
        {
            return document;
        }

        var generated = await _chunker
            .ChunkAsync(document, document.ExtractedText, cancellationToken)
            .ConfigureAwait(false);

        if (generated.Count > 0)
        {
            await _chunks.ReplaceForDocumentAsync(documentId, generated, cancellationToken).ConfigureAwait(false);
        }

        document.IsChunked = generated.Count > 0;
        document.ChunkCount = generated.Count;
        document.ChunkedAt = generated.Count > 0 ? DateTimeOffset.UtcNow : null;
        document.IsEmbedded = false;
        document.EmbeddedChunkCount = 0;
        document.EmbeddingModel = null;
        document.EmbeddedAt = null;
        document.UpdatedAt = DateTimeOffset.UtcNow;

        await _documents.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created {ChunkCount} chunks for document {DocumentId}.",
            generated.Count,
            documentId);

        if (generated.Count > 0)
        {
            try
            {
                document = await _embedding
                    .GenerateAndPersistAsync(documentId, userId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding generation failed for document {DocumentId}.", documentId);
            }
        }

        return document;
    }
}
