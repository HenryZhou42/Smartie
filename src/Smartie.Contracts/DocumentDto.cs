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
    string? ExtractionError,
    bool IsChunked,
    int ChunkCount,
    DateTimeOffset? ChunkedAt,
    string ChunkingStatusLabel,
    bool IsEmbedded,
    int EmbeddedChunkCount,
    string? EmbeddingModel,
    DateTimeOffset? EmbeddedAt,
    string EmbeddingStatusLabel,
    bool IsSample);

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
    string TextPreview,
    bool IsChunked,
    int ChunkCount,
    DateTimeOffset? ChunkedAt,
    string ChunkingStatusLabel,
    IReadOnlyList<DocumentChunkPreviewDto> ChunkPreviews,
    DocumentChunkDeveloperDto? ChunkDeveloper,
    bool IsEmbedded,
    int EmbeddedChunkCount,
    string? EmbeddingModel,
    DateTimeOffset? EmbeddedAt,
    string EmbeddingStatusLabel,
    DocumentEmbeddingDeveloperDto? EmbeddingDeveloper);

public sealed record DocumentChunkPreviewDto(
    int ChunkIndex,
    string Preview,
    int CharacterCount,
    int? PageNumber);

public sealed record DocumentChunkDeveloperDto(
    int TargetChunkSize,
    int ChunkOverlap,
    int ChunkCount,
    double AverageChunkLength);

public sealed record DocumentStatsDto(
    int DocumentCount,
    int CollectionCount,
    int IndexedCount,
    long TotalSizeBytes,
    int RecentUploadCount,
    int ExtractedCount,
    long TotalExtractedCharacters,
    DateTimeOffset? LastExtractedAt,
    string? LastExtractorUsed,
    int ChunkedCount,
    int TotalChunkCount,
    int EmbeddedDocumentCount,
    int TotalEmbeddedChunkCount);

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

public sealed record DocumentChunkingDeveloperDto(
    int TargetChunkSize,
    int ChunkOverlap,
    int ChunkedCount,
    int TotalChunkCount,
    double AverageChunkLength);

public sealed record DocumentEmbeddingDeveloperDto(
    string EmbeddingProvider,
    string EmbeddingModel,
    int? VectorDimension,
    int GeneratedCount,
    int FailedCount);
