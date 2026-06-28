namespace Smartie.Contracts;

public sealed record DocumentDto(
    Guid Id,
    string Name,
    string FileName,
    string Extension,
    string TypeLabel,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    DateTimeOffset UpdatedAt,
    bool IsIndexed,
    Guid? CollectionId,
    int TagCount,
    string ExtractionStatus,
    string ExtractionStatusLabel,
    bool IsExtracted,
    int ExtractedLength,
    DateTimeOffset? ExtractedAt,
    string? ExtractorUsed,
    long? ExtractionDurationMs,
    string? ExtractionError);

public sealed record DocumentDetailDto(
    Guid Id,
    string Name,
    string FileName,
    string Extension,
    string TypeLabel,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    DateTimeOffset UpdatedAt,
    string ExtractionStatus,
    string ExtractionStatusLabel,
    int ExtractedLength,
    DateTimeOffset? ExtractedAt,
    string? ExtractorUsed,
    long? ExtractionDurationMs,
    string? ExtractionError,
    string TextPreview);

public sealed record DocumentStatsDto(
    int DocumentCount,
    int CollectionCount,
    int IndexedCount,
    long TotalSizeBytes,
    int RecentUploadCount,
    int ExtractedCount,
    long TotalExtractedCharacters,
    DateTimeOffset? LastExtractedAt,
    string? LastExtractorUsed);

public sealed record RenameDocumentRequest(string Name);

public sealed record KnowledgeBaseSettingsDto(
    string StorageFolder,
    long MaxFileSizeBytes,
    string? DefaultCollection,
    IReadOnlyList<string> SupportedExtensions,
    IReadOnlyList<string> FutureExtensions);

public sealed record DocumentExtractionDeveloperDto(
    int ExtractedCount,
    long TotalExtractedCharacters,
    DateTimeOffset? LastExtractedAt,
    string? LastExtractorUsed,
    long? LastExtractionDurationMs);
