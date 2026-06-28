namespace Smartie.Domain.Entities;

/// <summary>
/// A chat session belonging to a <see cref="User"/>, containing an ordered list
/// of <see cref="Message"/> turns.
/// </summary>
public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string Title { get; set; } = "New conversation";

    public bool IsPinned { get; set; }

    public DateTimeOffset? PinnedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
