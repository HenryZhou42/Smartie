namespace Smartie.Domain.Entities;

/// <summary>
/// A recently accessed local file path tracked for desktop convenience.
/// </summary>
public class RecentFile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public bool Pinned { get; set; }

    public bool IsFavorite { get; set; }

    public DateTimeOffset LastOpenedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
