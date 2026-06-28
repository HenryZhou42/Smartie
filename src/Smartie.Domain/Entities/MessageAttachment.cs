namespace Smartie.Domain.Entities;

/// <summary>
/// Links a knowledge-base document or a direct chat upload to a chat message.
/// </summary>
public class MessageAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MessageId { get; set; }

    public Message? Message { get; set; }

    public Guid ConversationId { get; set; }

    public Conversation? Conversation { get; set; }

    public Guid? DocumentId { get; set; }

    public Document? Document { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public MessageAttachmentSourceType SourceType { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
