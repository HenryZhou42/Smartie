namespace Smartie.Domain.Entities;

/// <summary>
/// A slice of extracted document text stored for future retrieval and embeddings.
/// </summary>
public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    public Document? Document { get; set; }

    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    public int CharacterCount { get; set; }

    public int TokenEstimate { get; set; }

    public int StartPosition { get; set; }

    public int EndPosition { get; set; }

    public int? PageNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public byte[]? EmbeddingVector { get; set; }

    public string? EmbeddingModel { get; set; }

    public DateTimeOffset? EmbeddingGeneratedAt { get; set; }

    public ChunkEmbeddingStatus EmbeddingStatus { get; set; } = ChunkEmbeddingStatus.Pending;
}
