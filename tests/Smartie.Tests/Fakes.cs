using System.Runtime.CompilerServices;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Tests;

/// <summary>An in-memory <see cref="IConversationRepository"/> for fast unit tests.</summary>
internal sealed class InMemoryConversationRepository : IConversationRepository
{
    private readonly Dictionary<Guid, Conversation> _conversations = new();

    public Task<IReadOnlyList<Conversation>> ListAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Conversation>>(
            _conversations.Values
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.IsPinned)
                .ThenByDescending(c => c.IsPinned ? (c.PinnedAt ?? c.UpdatedAt) : c.UpdatedAt)
                .ToList());

    public Task<Conversation?> FindAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (!_conversations.TryGetValue(conversationId, out var conversation))
        {
            return Task.FromResult<Conversation?>(null);
        }

        var copy = new Conversation
        {
            Id = conversation.Id,
            UserId = conversation.UserId,
            Title = conversation.Title,
            IsPinned = conversation.IsPinned,
            PinnedAt = conversation.PinnedAt,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt,
            Messages = conversation.Messages
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.Id)
                .Select(m => new Message
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    Role = m.Role,
                    Content = m.Content,
                    GenerationStatus = m.GenerationStatus,
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt,
                    IsEdited = m.IsEdited,
                    EditedAt = m.EditedAt
                })
                .ToList()
        };

        return Task.FromResult<Conversation?>(copy);
    }

    public Task<Conversation> CreateAsync(Guid userId, string title, CancellationToken cancellationToken = default)
    {
        var conversation = new Conversation { UserId = userId, Title = title };
        _conversations[conversation.Id] = conversation;
        return Task.FromResult(conversation);
    }

    public Task<Message> AddMessageAsync(
        Guid conversationId,
        MessageRole role,
        string content,
        CancellationToken cancellationToken = default,
        MessageGenerationStatus generationStatus = MessageGenerationStatus.Complete)
    {
        var now = DateTimeOffset.UtcNow;
        var message = new Message
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            GenerationStatus = role == MessageRole.Assistant ? generationStatus : MessageGenerationStatus.Complete,
            CreatedAt = now,
            UpdatedAt = now
        };
        if (_conversations.TryGetValue(conversationId, out var conversation))
        {
            conversation.Messages.Add(message);
            conversation.UpdatedAt = now;
        }

        return Task.FromResult(message);
    }

    public Task UpdateTitleAsync(Guid conversationId, string title, CancellationToken cancellationToken = default)
    {
        if (_conversations.TryGetValue(conversationId, out var conversation))
        {
            conversation.Title = title;
            conversation.UpdatedAt = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task SetPinnedAsync(Guid conversationId, bool isPinned, CancellationToken cancellationToken = default)
    {
        if (_conversations.TryGetValue(conversationId, out var conversation))
        {
            var now = DateTimeOffset.UtcNow;
            conversation.IsPinned = isPinned;
            conversation.PinnedAt = isPinned ? now : null;
            conversation.UpdatedAt = now;
        }

        return Task.CompletedTask;
    }

    public Task<Message?> EditUserMessageAndTruncateAsync(
        Guid conversationId,
        Guid messageId,
        string newContent,
        CancellationToken cancellationToken = default)
    {
        if (!_conversations.TryGetValue(conversationId, out var conversation))
        {
            return Task.FromResult<Message?>(null);
        }

        var ordered = conversation.Messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList();
        var index = ordered.FindIndex(m => m.Id == messageId);
        if (index < 0)
        {
            return Task.FromResult<Message?>(null);
        }

        var target = ordered[index];
        if (target.Role != MessageRole.User)
        {
            throw new InvalidOperationException("Only user messages can be edited.");
        }

        var now = DateTimeOffset.UtcNow;
        target.Content = newContent.Trim();
        target.IsEdited = true;
        target.EditedAt = now;
        target.UpdatedAt = now;

        foreach (var remove in ordered.Skip(index + 1))
        {
            conversation.Messages.Remove(remove);
        }

        conversation.UpdatedAt = now;
        return Task.FromResult<Message?>(target);
    }

    public Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_conversations.Remove(conversationId));
}

/// <summary>An <see cref="IChatAiService"/> that streams scripted chunks.</summary>
internal sealed class FakeChatAiService : IChatAiService
{
    private readonly string[] _chunks;

    public FakeChatAiService(params string[] chunks) => _chunks = chunks;

    public IReadOnlyList<Message>? LastHistory { get; private set; }

    public async IAsyncEnumerable<string> StreamReplyAsync(
        IReadOnlyList<Message> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LastHistory = history;
        foreach (var chunk in _chunks)
        {
            await Task.Yield();
            yield return chunk;
        }
    }
}

internal sealed class InMemoryMessageAttachmentRepository : IMessageAttachmentRepository
{
    private readonly Dictionary<Guid, List<MessageAttachment>> _byMessage = new();

    public Task AddKnowledgeBaseAttachmentsAsync(
        Guid messageId,
        Guid conversationId,
        IReadOnlyList<Guid> documentIds,
        CancellationToken cancellationToken = default)
    {
        if (!_byMessage.TryGetValue(messageId, out var list))
        {
            list = new List<MessageAttachment>();
            _byMessage[messageId] = list;
        }

        foreach (var documentId in documentIds)
        {
            list.Add(new MessageAttachment
            {
                MessageId = messageId,
                ConversationId = conversationId,
                DocumentId = documentId,
                SourceType = MessageAttachmentSourceType.KnowledgeBase
            });
        }

        return Task.CompletedTask;
    }

    public Task AddLocalUploadAttachmentsAsync(
        Guid messageId,
        Guid conversationId,
        IReadOnlyList<MessageAttachment> attachments,
        CancellationToken cancellationToken = default)
    {
        if (!_byMessage.TryGetValue(messageId, out var list))
        {
            list = new List<MessageAttachment>();
            _byMessage[messageId] = list;
        }

        list.AddRange(attachments);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MessageAttachment>> GetForMessageAsync(
        Guid messageId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MessageAttachment>>(
            _byMessage.TryGetValue(messageId, out var list)
                ? list.ToList()
                : Array.Empty<MessageAttachment>());
}

internal sealed class PassthroughAttachedDocumentPromptBuilder : IAttachedDocumentPromptBuilder
{
    public Task<string> BuildAugmentedUserMessageAsync(
        Guid userId,
        string userMessage,
        IReadOnlyList<MessageAttachment> attachments,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(userMessage);
}

internal sealed class NoOpMemoryService : IMemoryService
{
    public Task<Memory> StoreMemoryAsync(
        Guid userId,
        string content,
        MemoryCategory category,
        MemoryImportance importance,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new Memory { Content = content, Category = category, Importance = importance });

    public Task<IReadOnlyList<MemorySearchResult>> SearchMemoryAsync(
        Guid userId,
        string query,
        int topK,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MemorySearchResult>>(Array.Empty<MemorySearchResult>());

    public Task<bool> DeleteMemoryAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<Memory?> PinMemoryAsync(
        Guid userId,
        Guid memoryId,
        bool pinned,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Memory?>(null);

    public Task<Memory?> UpdateMemoryAsync(
        Guid userId,
        Guid memoryId,
        string content,
        MemoryCategory category,
        MemoryImportance importance,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Memory?>(null);

    public Task<IReadOnlyList<Memory>> ListMemoriesAsync(
        Guid userId,
        MemoryCategory? category,
        bool? pinnedOnly,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Memory>>(Array.Empty<Memory>());

    public Task ExtractAndStoreFromUserMessageAsync(
        Guid userId,
        string userMessage,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<MemorySettingsSnapshot> GetSettingsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new MemorySettingsSnapshot(true, 200, 365, 0));

    public Task UpdateSettingsAsync(
        Guid userId,
        MemorySettingsUpdate update,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<MemoryDeveloperStats> GetDeveloperStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new MemoryDeveloperStats(0, 0, null, 5, 45));
}

internal sealed class NoOpMemoryPromptBuilder : IMemoryPromptBuilder
{
    public Task<(string? PromptBlock, MemoryRetrievalDiagnostics Diagnostics)> BuildMemoryContextAsync(
        Guid userId,
        string query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<(string?, MemoryRetrievalDiagnostics)>(
            (null, new MemoryRetrievalDiagnostics(0, 0, null, 0, 0)));
}

internal sealed class RecordingAttachedDocumentPromptBuilder : IAttachedDocumentPromptBuilder
{
    public IReadOnlyList<MessageAttachment>? LastAttachments { get; private set; }
    public string? LastUserMessage { get; private set; }
    public string? LastAugmentedMessage { get; private set; }

    public Task<string> BuildAugmentedUserMessageAsync(
        Guid userId,
        string userMessage,
        IReadOnlyList<MessageAttachment> attachments,
        CancellationToken cancellationToken = default)
    {
        LastAttachments = attachments.ToList();
        LastUserMessage = userMessage;
        LastAugmentedMessage = $"AUGMENTED:{userMessage}";
        return Task.FromResult(LastAugmentedMessage);
    }
}

internal sealed class FakeChatAttachmentStorage : IChatAttachmentStorage
{
    public Task<ChatAttachmentFileInfo> SaveStagingAsync(
        Guid conversationId,
        Guid stagingId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<ChatAttachmentFileInfo>> CommitStagingAsync(
        Guid conversationId,
        Guid stagingId,
        Guid messageId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ChatAttachmentFileInfo>>(Array.Empty<ChatAttachmentFileInfo>());

    public Task DeleteStagingAsync(
        Guid conversationId,
        Guid stagingId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public string GetAbsolutePath(string relativePath) => relativePath;

    public bool StagingExists(Guid conversationId, Guid stagingId) => false;
}
