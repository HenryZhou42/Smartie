namespace Smartie.Contracts;

/// <summary>Summary of a conversation for list/detail views.</summary>
public sealed record ConversationDto(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsPinned = false,
    DateTimeOffset? PinnedAt = null);
