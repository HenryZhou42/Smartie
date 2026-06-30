namespace Smartie.Contracts;

public sealed record SemanticSearchRequest(string Query, int? TopK = null);

public sealed record SemanticSearchResultDto(
    Guid DocumentId,
    Guid ChunkId,
    float Score,
    string Content,
    string Preview,
    string FileName,
    int? PageNumber,
    int ChunkIndex);

public sealed record SemanticSearchResponseDto(
    IReadOnlyList<SemanticSearchResultDto> Results,
    SemanticSearchDeveloperDto Developer);

public sealed record SemanticSearchDeveloperDto(
    int TopK,
    int? EmbeddingDimensions,
    long SearchDurationMs,
    string QueryEmbeddingProvider,
    float? TopScore);

public sealed record SemanticSearchSettingsDeveloperDto(
    int DefaultTopK,
    IReadOnlyList<int> AllowedTopKValues,
    int MinSimilarityScorePercent,
    int? EmbeddingDimensions,
    int EmbeddedChunkCount);
