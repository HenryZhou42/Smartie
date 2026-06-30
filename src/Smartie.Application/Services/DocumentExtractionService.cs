using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public sealed class DocumentExtractionService : IDocumentExtractionService
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentTextExtractionRouter _router;
    private readonly IDocumentChunkingService _chunking;
    private readonly ILogger<DocumentExtractionService> _logger;

    public DocumentExtractionService(
        IDocumentRepository repository,
        IDocumentTextExtractionRouter router,
        IDocumentChunkingService chunking,
        ILogger<DocumentExtractionService> logger)
    {
        _repository = repository;
        _router = router;
        _chunking = chunking;
        _logger = logger;
    }

    public async Task<Document> ExtractAndPersistAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var document = await _repository
            .FindForUpdateAsync(documentId, userId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Document {documentId} was not found.");

        if (document.ExtractionStatus == DocumentExtractionStatus.Completed &&
            !string.IsNullOrEmpty(document.ExtractedText))
        {
            return document;
        }

        document.ExtractionStatus = DocumentExtractionStatus.Extracting;
        document.ExtractionError = null;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var extractorName = _router.GetExtractorName(document);
        if (extractorName is null)
        {
            document.ExtractionStatus = DocumentExtractionStatus.Failed;
            document.ExtractionError = $"No extractor is registered for extension '{document.Extension}'.";
            document.UpdatedAt = DateTimeOffset.UtcNow;
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return document;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var text = await _router.ExtractTextAsync(document, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            document.ExtractedText = text;
            document.ExtractedLength = text.Length;
            document.ExtractedAt = DateTimeOffset.UtcNow;
            document.ExtractionStatus = DocumentExtractionStatus.Completed;
            document.ExtractorUsed = extractorName;
            document.ExtractionDurationMs = stopwatch.ElapsedMilliseconds;
            document.ExtractionError = null;
            document.UpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Extracted {CharacterCount} characters from document {DocumentId} using {Extractor} in {DurationMs}ms.",
                text.Length,
                documentId,
                extractorName,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            document.ExtractionStatus = DocumentExtractionStatus.Failed;
            document.ExtractionError = ex.Message;
            document.ExtractorUsed = extractorName;
            document.ExtractionDurationMs = stopwatch.ElapsedMilliseconds;
            document.UpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogWarning(ex, "Text extraction failed for document {DocumentId}.", documentId);
        }

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (document.ExtractionStatus == DocumentExtractionStatus.Completed &&
            !string.IsNullOrWhiteSpace(document.ExtractedText))
        {
            try
            {
                document = await _chunking
                    .ChunkAndPersistAsync(documentId, userId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chunking failed for document {DocumentId}.", documentId);
            }
        }

        return document;
    }
}
