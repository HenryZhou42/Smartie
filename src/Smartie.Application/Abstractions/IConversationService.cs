using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

/// <summary>
/// Application-facing orchestration for conversations: listing, creating, and
/// driving the chat exchange (persist user message, stream + persist reply).
/// </summary>
public interface IConversationService
{
    Task<IReadOnlyList<Conversation>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Conversation?> GetAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<Conversation> CreateAsync(Guid userId, string? title, CancellationToken cancellationToken = default);

    Task SetPinnedAsync(Guid conversationId, bool isPinned, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the user's message, then streams the assistant reply as text chunks,
    /// persisting the full reply when streaming completes.
    /// </summary>
    IAsyncEnumerable<string> StreamReplyAsync(
        Guid conversationId,
        string userInput,
        IReadOnlyList<Guid>? attachmentDocumentIds = null,
        IReadOnlyList<Guid>? stagingAttachmentIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits a user message and removes all following turns without generating a reply.
    /// </summary>
    Task<IReadOnlyList<Message>> EditUserMessageAsync(
        Guid conversationId,
        Guid messageId,
        string newContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a new assistant reply from the current persisted conversation history.
    /// </summary>
    IAsyncEnumerable<string> StreamRegenerateAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits a user message, removes all following turns, then streams a new assistant reply.
    /// </summary>
    IAsyncEnumerable<string> StreamEditAndRegenerateAsync(
        Guid conversationId,
        Guid messageId,
        string newContent,
        CancellationToken cancellationToken = default);

    /// <summary>Non-streaming variant; returns the full assistant message.</summary>
    Task<Message> SendAsync(
        Guid conversationId,
        string userInput,
        CancellationToken cancellationToken = default);
}
