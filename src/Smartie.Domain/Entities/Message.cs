namespace Smartie.Domain.Entities;

/// <summary>
/// A single turn within a <see cref="Conversation"/>.
/// </summary>
public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }

    public Conversation? Conversation { get; set; }

    public MessageRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>Only meaningful for assistant messages; user messages are always complete.</summary>
    public MessageGenerationStatus GenerationStatus { get; set; } = MessageGenerationStatus.Complete;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsEdited { get; set; }

    public DateTimeOffset? EditedAt { get; set; }

    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
}
