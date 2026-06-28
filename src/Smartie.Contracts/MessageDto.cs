namespace Smartie.Contracts;

/// <summary>A single chat turn. <see cref="Role"/> is "user", "assistant", "system" or "tool".</summary>
public sealed record MessageDto(
    Guid Id,
    string Role,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt = null,
    bool IsEdited = false,
    DateTimeOffset? EditedAt = null,
    string? Status = null,
    IReadOnlyList<MessageAttachmentDto>? Attachments = null);
