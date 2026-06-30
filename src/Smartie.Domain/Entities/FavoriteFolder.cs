namespace Smartie.Domain.Entities;

/// <summary>
/// A user-selected folder for desktop file browsing and search.
/// </summary>
public class FavoriteFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string FolderPath { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
}
