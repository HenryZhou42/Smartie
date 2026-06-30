namespace Smartie.Domain.Entities;

/// <summary>
/// Tracks command palette usage for ranking and recency.
/// </summary>
public class RecentCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string CommandName { get; set; } = string.Empty;

    public int UsageCount { get; set; }

    public DateTimeOffset LastUsed { get; set; } = DateTimeOffset.UtcNow;
}
