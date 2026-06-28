namespace Smartie.Domain.Entities;

/// <summary>
/// A file stored in the local Smartie knowledge base (metadata in SQLite, bytes on disk).
/// </summary>
public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>Display name shown in the UI (may differ from stored file name).</summary>
    public string Name { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    /// <summary>Relative path under the Smartie documents root (e.g. "{id}/file.pdf").</summary>
    public string RelativePath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsIndexed { get; set; }

    public Guid? CollectionId { get; set; }

    public int TagCount { get; set; }

    public string? ExtractedText { get; set; }

    public int ExtractedLength { get; set; }

    public DateTimeOffset? ExtractedAt { get; set; }

    public DocumentExtractionStatus ExtractionStatus { get; set; } = DocumentExtractionStatus.Pending;

    /// <summary>Name of the extractor implementation that produced <see cref="ExtractedText"/>.</summary>
    public string? ExtractorUsed { get; set; }

    public long? ExtractionDurationMs { get; set; }

    public string? ExtractionError { get; set; }

    public User? User { get; set; }
}
