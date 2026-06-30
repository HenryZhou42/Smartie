namespace Smartie.Application.Abstractions;

public sealed record SearchResult(
    Guid DocumentId,
    Guid ChunkId,
    float Score,
    string Content,
    string FileName,
    int? PageNumber,
    int ChunkIndex);

public sealed record SemanticSearchDiagnostics(
    long SearchDurationMs,
    int TopK,
    int? EmbeddingDimensions,
    string EmbeddingProvider,
    float? TopScore);

public sealed record SemanticSearchResultSet(
    IReadOnlyList<SearchResult> Results,
    SemanticSearchDiagnostics Diagnostics);

public sealed record SearchableChunkRow(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string FileName,
    int ChunkIndex,
    string Content,
    int? PageNumber,
    byte[] EmbeddingVector);
