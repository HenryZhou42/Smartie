namespace Smartie.Contracts;

/// <summary>Create a new conversation. Title is optional (defaulted server-side).</summary>
public sealed record CreateConversationRequest(string? Title = null);

/// <summary>Send a user message into a conversation.</summary>
public sealed record SendMessageRequest(
    string Content,
    IReadOnlyList<Guid>? DocumentIds = null,
    IReadOnlyList<Guid>? StagingAttachmentIds = null);

/// <summary>A file staged for attachment before the message is sent.</summary>
public sealed record StagingChatAttachmentDto(
    Guid StagingId,
    string FileName,
    string Extension,
    string TypeLabel,
    long SizeBytes);

/// <summary>Pin or unpin a conversation.</summary>
public sealed record SetConversationPinnedRequest(bool IsPinned);

/// <summary>Edit a user message and regenerate the assistant reply from that point.</summary>
public sealed record EditMessageRequest(string Content);

/// <summary>A streamed slice of an assistant reply (used by the SSE endpoint).</summary>
public sealed record ChatChunk(string Delta);
