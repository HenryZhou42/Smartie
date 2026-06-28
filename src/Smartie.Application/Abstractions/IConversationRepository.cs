using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

/// <summary>
/// Persistence boundary for conversations and their messages.
/// </summary>
public interface IConversationRepository
{
    Task<IReadOnlyList<Conversation>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Loads a conversation including its messages (ordered), or null.</summary>
    Task<Conversation?> FindAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<Conversation> CreateAsync(Guid userId, string title, CancellationToken cancellationToken = default);

    Task<Message> AddMessageAsync(
        Guid conversationId,
        MessageRole role,
        string content,
        CancellationToken cancellationToken = default,
        MessageGenerationStatus generationStatus = MessageGenerationStatus.Complete);

    Task UpdateTitleAsync(Guid conversationId, string title, CancellationToken cancellationToken = default);

    Task SetPinnedAsync(Guid conversationId, bool isPinned, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a user message, marks it edited, and removes all following messages in the conversation.
    /// Returns null when the message is not found.
    /// </summary>
    Task<Message?> EditUserMessageAndTruncateAsync(
        Guid conversationId,
        Guid messageId,
        string newContent,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
