using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public sealed record MemorySearchResult(
    Guid MemoryId,
    string Content,
    MemoryCategory Category,
    MemoryImportance Importance,
    float Score,
    bool Pinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastReferencedAt);

public sealed record SearchableMemoryRow(
    Guid MemoryId,
    string Content,
    MemoryCategory Category,
    MemoryImportance Importance,
    bool Pinned,
    byte[] EmbeddingVector,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastReferencedAt);

public sealed record MemoryRetrievalDiagnostics(
    int MemoryHits,
    int RetrievedCount,
    float? TopScore,
    long SearchDurationMs,
    int TotalMemoryCount);

public sealed record MemorySettingsSnapshot(
    bool Enabled,
    int MaxMemories,
    int RetentionDays,
    int CurrentCount);

public sealed record MemorySettingsUpdate(
    bool? Enabled,
    int? MaxMemories,
    int? RetentionDays);

public sealed record MemoryDeveloperStats(
    int MemoryCount,
    int PinnedCount,
    int? EmbeddingDimensions,
    int DefaultSearchTopK,
    int MinSimilarityScorePercent);
