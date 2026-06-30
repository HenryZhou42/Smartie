namespace Smartie.Domain.Entities;

/// <summary>
/// A persistent personal fact Smartie remembers across conversations.
/// </summary>
public class Memory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string Content { get; set; } = string.Empty;

    public MemoryCategory Category { get; set; } = MemoryCategory.Custom;

    public MemoryImportance Importance { get; set; } = MemoryImportance.Medium;

    public byte[]? EmbeddingVector { get; set; }

    public string? EmbeddingModel { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool Pinned { get; set; }

    public DateTimeOffset? LastReferencedAt { get; set; }
}
