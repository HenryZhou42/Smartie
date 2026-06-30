using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Automation;
using Smartie.Application.Configuration;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public sealed class DocumentService : IDocumentService
{
    private const int TextPreviewLength = 1000;

    private static readonly string[] FutureExtensions =
        [".png", ".jpg", ".jpeg", ".pptx", ".xlsx", ".html", ".htm"];

    private readonly IDocumentRepository _repository;
    private readonly IDocumentStorage _storage;
    private readonly IDocumentExtractionService _extraction;
    private readonly IAutomationEventPublisher _automations;
    private readonly KnowledgeBaseOptions _options;

    public DocumentService(
        IDocumentRepository repository,
        IDocumentStorage storage,
        IDocumentExtractionService extraction,
        IAutomationEventPublisher automations,
        IOptions<KnowledgeBaseOptions> options)
    {
        _repository = repository;
        _storage = storage;
        _extraction = extraction;
        _automations = automations;
        _options = options.Value;
    }

    public Task<IReadOnlyList<Document>> ListAsync(
        Guid userId,
        string? search,
        CancellationToken cancellationToken = default) =>
        _repository.ListAsync(userId, search, cancellationToken);

    public Task<Document?> GetAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default) =>
        _repository.FindAsync(documentId, userId, cancellationToken);

    public async Task<DocumentStats> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _repository.GetStatsAsync(userId, cancellationToken).ConfigureAwait(false);

    public async Task<Document> UploadAsync(
        Guid userId,
        string originalFileName,
        Stream content,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException("File name is required.", nameof(originalFileName));
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentException("File is empty.", nameof(sizeBytes));
        }

        if (sizeBytes > _options.MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"File exceeds the maximum size of {FormatBytes(_options.MaxFileSizeBytes)}.");
        }

        var extension = Path.GetExtension(originalFileName);
        if (!IsAllowedExtension(extension))
        {
            throw new InvalidOperationException(
                $"File type '{extension}' is not supported. Allowed: {string.Join(", ", _options.AllowedExtensions)}.");
        }

        var documentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var relativePath = await _storage
            .SaveAsync(documentId, originalFileName, content, cancellationToken)
            .ConfigureAwait(false);

        var displayName = Path.GetFileNameWithoutExtension(originalFileName);
        var storedFileName = Path.GetFileName(relativePath.Split('/').Last());

        var document = new Document
        {
            Id = documentId,
            UserId = userId,
            Name = displayName,
            FileName = storedFileName,
            Extension = extension.TrimStart('.').ToLowerInvariant(),
            RelativePath = relativePath,
            SizeBytes = sizeBytes,
            UploadedAt = now,
            UpdatedAt = now,
            IsIndexed = false,
            TagCount = 0,
            ExtractionStatus = DocumentExtractionStatus.Pending
        };

        await _repository.AddAsync(document, cancellationToken).ConfigureAwait(false);
        var extracted = await _extraction.ExtractAndPersistAsync(documentId, userId, cancellationToken).ConfigureAwait(false);

        await _automations.PublishAsync(
            userId,
            AutomationTriggerType.KnowledgeBaseUpdated,
            new AutomationEventContext(
                DocumentType: extracted.Extension,
                DocumentId: extracted.Id,
                Keyword: extracted.Name),
            cancellationToken).ConfigureAwait(false);

        return extracted;
    }

    public async Task<Document?> RenameAsync(
        Guid userId,
        Guid documentId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var trimmed = newName.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Name must not be empty.", nameof(newName));
        }

        return await _repository
            .UpdateNameAsync(documentId, userId, trimmed, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _repository.FindAsync(documentId, userId, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return false;
        }

        await _storage.DeleteAsync(document.RelativePath, cancellationToken).ConfigureAwait(false);
        return await _repository.DeleteAsync(documentId, userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(Document Document, string AbsolutePath)?> GetForOpenAsync(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.FindAsync(documentId, userId, cancellationToken).ConfigureAwait(false);
        if (document is null || !_storage.Exists(document.RelativePath))
        {
            return null;
        }

        return (document, _storage.GetAbsolutePath(document.RelativePath));
    }

    public KnowledgeBaseSettingsSnapshot GetSettings() =>
        new(
            _storage.GetStorageRoot(),
            _options.MaxFileSizeBytes,
            DefaultCollection: null,
            _options.AllowedExtensions,
            FutureExtensions);

    public static string BuildTextPreview(string? text) =>
        string.IsNullOrEmpty(text)
            ? string.Empty
            : text.Length <= TextPreviewLength
                ? text
                : text[..TextPreviewLength] + "…";

    public static string GetExtractionStatusLabel(DocumentExtractionStatus status) =>
        status switch
        {
            DocumentExtractionStatus.Pending => "Pending",
            DocumentExtractionStatus.Extracting => "Extracting",
            DocumentExtractionStatus.Completed => "Completed",
            DocumentExtractionStatus.Failed => "Failed",
            _ => status.ToString()
        };

    public static string GetChunkingStatusLabel(Document document) =>
        document.IsChunked && document.ChunkCount > 0
            ? "Chunked"
            : document.ExtractionStatus == DocumentExtractionStatus.Completed
                ? "Not Chunked"
                : "Pending";

    public static string GetEmbeddingStatusLabel(Document document) =>
        !document.IsChunked || document.ChunkCount <= 0
            ? "Pending"
            : document.IsEmbedded
                ? "Embedded"
                : document.EmbeddedChunkCount > 0
                    ? $"Partial ({document.EmbeddedChunkCount}/{document.ChunkCount})"
                    : "Not Embedded";

    public static string BuildChunkPreview(string content, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = string.Join(' ', content.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength].TrimEnd() + "…";
    }

    private bool IsAllowedExtension(string extension) =>
        _options.AllowedExtensions.Any(e =>
            string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));

    public static string GetTypeLabel(string extension) =>
        extension.ToLowerInvariant() switch
        {
            "pdf" => "PDF",
            "docx" => "DOCX",
            "txt" => "TXT",
            "md" or "markdown" => "Markdown",
            _ => extension.ToUpperInvariant()
        };

    internal static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }
}
