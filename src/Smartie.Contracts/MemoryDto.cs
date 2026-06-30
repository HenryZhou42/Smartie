namespace Smartie.Contracts;

public sealed record MemoryDto(
    Guid Id,
    string Content,
    string Category,
    string Importance,
    bool Pinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastReferencedAt);

public sealed record CreateMemoryRequest(
    string Content,
    string Category,
    string Importance);

public sealed record UpdateMemoryRequest(
    string Content,
    string Category,
    string Importance);

public sealed record PinMemoryRequest(bool Pinned);

public sealed record MemorySearchRequest(string Query, int? TopK = null);

public sealed record MemorySearchResultDto(
    Guid MemoryId,
    string Content,
    string Category,
    string Importance,
    float Score,
    bool Pinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastReferencedAt);

public sealed record MemorySearchResponseDto(
    IReadOnlyList<MemorySearchResultDto> Results,
    MemorySearchDeveloperDto Developer);

public sealed record MemorySearchDeveloperDto(
    int MemoryHits,
    int RetrievedCount,
    float? TopScore,
    long SearchDurationMs,
    int TotalMemoryCount);

public sealed record MemorySettingsDto(
    bool Enabled,
    int MaxMemories,
    int RetentionDays,
    int CurrentCount);

public sealed record UpdateMemorySettingsRequest(
    bool? Enabled,
    int? MaxMemories,
    int? RetentionDays);

public sealed record MemoryDeveloperDto(
    int MemoryCount,
    int PinnedCount,
    int? EmbeddingDimensions,
    int DefaultSearchTopK,
    int MinSimilarityScorePercent);
