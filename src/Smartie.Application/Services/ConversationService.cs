using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

/// <summary>
/// Coordinates the chat flow over the repository and the AI provider.
/// </summary>
public sealed class ConversationService : IConversationService
{
    private const string DefaultTitle = "New conversation";

    private readonly IConversationRepository _repository;
    private readonly IMessageAttachmentRepository _attachments;
    private readonly IChatAttachmentStorage _chatAttachmentStorage;
    private readonly IAttachedDocumentPromptBuilder _documentPromptBuilder;
    private readonly IMemoryService _memory;
    private readonly IMemoryPromptBuilder _memoryPromptBuilder;
    private readonly IChatAiService _ai;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        IConversationRepository repository,
        IMessageAttachmentRepository attachments,
        IChatAttachmentStorage chatAttachmentStorage,
        IAttachedDocumentPromptBuilder documentPromptBuilder,
        IMemoryService memory,
        IMemoryPromptBuilder memoryPromptBuilder,
        IChatAiService ai,
        ILogger<ConversationService> logger)
    {
        _repository = repository;
        _attachments = attachments;
        _chatAttachmentStorage = chatAttachmentStorage;
        _documentPromptBuilder = documentPromptBuilder;
        _memory = memory;
        _memoryPromptBuilder = memoryPromptBuilder;
        _ai = ai;
        _logger = logger;
    }

    public Task<IReadOnlyList<Conversation>> ListAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _repository.ListAsync(userId, cancellationToken);

    public Task<Conversation?> GetAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        _repository.FindAsync(conversationId, cancellationToken);

    public Task<Conversation> CreateAsync(Guid userId, string? title, CancellationToken cancellationToken = default) =>
        _repository.CreateAsync(
            userId,
            string.IsNullOrWhiteSpace(title) ? DefaultTitle : title!.Trim(),
            cancellationToken);

    public Task SetPinnedAsync(Guid conversationId, bool isPinned, CancellationToken cancellationToken = default) =>
        _repository.SetPinnedAsync(conversationId, isPinned, cancellationToken);

    public Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(conversationId, cancellationToken);

    public async IAsyncEnumerable<string> StreamReplyAsync(
        Guid conversationId,
        string userInput,
        IReadOnlyList<Guid>? attachmentDocumentIds = null,
        IReadOnlyList<Guid>? stagingAttachmentIds = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            throw new ArgumentException("User input must not be empty.", nameof(userInput));
        }

        var conversation = await _repository.FindAsync(conversationId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Conversation {conversationId} was not found.");

        var isFirstMessage = conversation.Messages.Count == 0;
        var trimmed = userInput.Trim();
        var documentIds = attachmentDocumentIds?.Where(id => id != Guid.Empty).Distinct().ToList()
            ?? new List<Guid>();
        var stagingIds = stagingAttachmentIds?.Where(id => id != Guid.Empty).Distinct().ToList()
            ?? new List<Guid>();

        var userMessage = await _repository
            .AddMessageAsync(conversationId, MessageRole.User, trimmed, cancellationToken)
            .ConfigureAwait(false);

        if (documentIds.Count > 0)
        {
            await _attachments.AddKnowledgeBaseAttachmentsAsync(
                    userMessage.Id,
                    conversationId,
                    documentIds,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (stagingIds.Count > 0)
        {
            var localAttachments = new List<MessageAttachment>();
            foreach (var stagingId in stagingIds)
            {
                if (!_chatAttachmentStorage.StagingExists(conversationId, stagingId))
                {
                    throw new KeyNotFoundException($"Staged attachment {stagingId} was not found.");
                }

                var committed = await _chatAttachmentStorage
                    .CommitStagingAsync(conversationId, stagingId, userMessage.Id, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var file in committed)
                {
                    localAttachments.Add(new MessageAttachment
                    {
                        OriginalFileName = file.OriginalFileName,
                        StoredFileName = file.StoredFileName,
                        FilePath = file.RelativePath,
                        Extension = file.Extension,
                        SizeBytes = file.SizeBytes
                    });
                }
            }

            if (localAttachments.Count > 0)
            {
                await _attachments.AddLocalUploadAttachmentsAsync(
                        userMessage.Id,
                        conversationId,
                        localAttachments,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (isFirstMessage && IsDefaultTitle(conversation.Title))
        {
            await _repository.UpdateTitleAsync(conversationId, BuildTitle(trimmed), cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            await _memory
                .ExtractAndStoreFromUserMessageAsync(conversation.UserId, trimmed, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Memory extraction failed for conversation {ConversationId}.", conversationId);
        }

        var refreshed = await _repository.FindAsync(conversationId, cancellationToken).ConfigureAwait(false);
        var history = refreshed?.Messages.ToList() ?? new List<Message>();
        var historyForAi = await BuildHistoryForAiAsync(conversation.UserId, history, cancellationToken)
            .ConfigureAwait(false);

        await foreach (var chunk in StreamAssistantReplyAsync(conversationId, historyForAi, cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    public async Task<IReadOnlyList<Message>> EditUserMessageAsync(
        Guid conversationId,
        Guid messageId,
        string newContent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newContent))
        {
            throw new ArgumentException("Message content must not be empty.", nameof(newContent));
        }

        var conversation = await _repository.FindAsync(conversationId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Conversation {conversationId} was not found.");

        _ = await _repository
            .EditUserMessageAndTruncateAsync(conversationId, messageId, newContent, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Message {messageId} was not found.");

        var refreshed = await _repository.FindAsync(conversationId, cancellationToken).ConfigureAwait(false);
        return refreshed?.Messages.ToList() ?? new List<Message>();
    }

    public async IAsyncEnumerable<string> StreamRegenerateAsync(
        Guid conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var conversation = await _repository.FindAsync(conversationId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Conversation {conversationId} was not found.");

        var history = conversation.Messages.ToList();
        var historyForAi = await BuildHistoryForAiAsync(conversation.UserId, history, cancellationToken)
            .ConfigureAwait(false);

        await foreach (var chunk in StreamAssistantReplyAsync(conversationId, historyForAi, cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    public async IAsyncEnumerable<string> StreamEditAndRegenerateAsync(
        Guid conversationId,
        Guid messageId,
        string newContent,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EditUserMessageAsync(conversationId, messageId, newContent, cancellationToken).ConfigureAwait(false);

        await foreach (var chunk in StreamRegenerateAsync(conversationId, cancellationToken).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    public async Task<Message> SendAsync(
        Guid conversationId,
        string userInput,
        CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        await foreach (var chunk in StreamReplyAsync(conversationId, userInput, cancellationToken: cancellationToken)
                           .ConfigureAwait(false))
        {
            builder.Append(chunk);
        }

        return new Message
        {
            ConversationId = conversationId,
            Role = MessageRole.Assistant,
            Content = builder.ToString()
        };
    }

    private async Task<List<Message>> BuildHistoryForAiAsync(
        Guid userId,
        IReadOnlyList<Message> history,
        CancellationToken cancellationToken)
    {
        if (history.Count == 0)
        {
            return new List<Message>();
        }

        var copy = history
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
            .ToList();

        var lastUser = copy.LastOrDefault(m => m.Role == MessageRole.User);
        var originalLastUser = history.LastOrDefault(m => m.Role == MessageRole.User);
        if (lastUser is null || originalLastUser is null)
        {
            return copy;
        }

        var (memoryBlock, diagnostics) = await _memoryPromptBuilder
            .BuildMemoryContextAsync(userId, lastUser.Content, cancellationToken)
            .ConfigureAwait(false);

        if (diagnostics.RetrievedCount > 0)
        {
            _logger.LogInformation(
                "Retrieved {MemoryCount} memories for prompt injection (top score {TopScore}).",
                diagnostics.RetrievedCount,
                diagnostics.TopScore);
        }

        var messageAttachments = originalLastUser.Attachments.Count > 0
            ? originalLastUser.Attachments.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToList()
            : await _attachments.GetForMessageAsync(originalLastUser.Id, cancellationToken).ConfigureAwait(false);

        var augmented = messageAttachments.Count > 0
            ? await _documentPromptBuilder
                .BuildAugmentedUserMessageAsync(userId, lastUser.Content, messageAttachments, cancellationToken)
                .ConfigureAwait(false)
            : lastUser.Content;

        if (!string.IsNullOrWhiteSpace(memoryBlock))
        {
            lastUser.Content = messageAttachments.Count > 0
                ? memoryBlock + Environment.NewLine + Environment.NewLine + augmented
                : memoryBlock + Environment.NewLine + Environment.NewLine + "User Question:" + Environment.NewLine + augmented;
        }
        else
        {
            lastUser.Content = augmented;
        }

        if (messageAttachments.Count > 0 || !string.IsNullOrWhiteSpace(memoryBlock))
        {
            _logger.LogInformation(
                "Provider will receive augmented user message of {ContentLength} characters for conversation history.",
                lastUser.Content.Length);
        }

        return copy;
    }

    private async IAsyncEnumerable<string> StreamAssistantReplyAsync(
        Guid conversationId,
        IReadOnlyList<Message> history,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        await using var enumerator = _ai.StreamReplyAsync(history, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                string chunk;
                string? errorMessage = null;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }

                    chunk = enumerator.Current;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (AiRateLimitedException ex)
                {
                    _logger.LogWarning(ex, "AI provider rate-limited for conversation {ConversationId}.", conversationId);
                    errorMessage = "\u26a0\ufe0f The AI provider is rate-limited or out of quota right now. Please wait a moment and try again.";
                    chunk = string.Empty;
                }
                catch (AiServiceException ex)
                {
                    _logger.LogError(ex, "AI provider error for conversation {ConversationId}.", conversationId);
                    errorMessage = "\u26a0\ufe0f " + ex.Message;
                    chunk = string.Empty;
                }

                if (errorMessage is not null)
                {
                    yield return errorMessage;
                    yield break;
                }

                builder.Append(chunk);
                yield return chunk;
            }
        }
        finally
        {
            var reply = builder.ToString();
            if (!string.IsNullOrEmpty(reply))
            {
                var status = cancellationToken.IsCancellationRequested
                    ? MessageGenerationStatus.Stopped
                    : MessageGenerationStatus.Complete;

                await _repository.AddMessageAsync(
                        conversationId,
                        MessageRole.Assistant,
                        reply,
                        CancellationToken.None,
                        status)
                    .ConfigureAwait(false);
            }
        }
    }

    private static bool IsDefaultTitle(string title) =>
        string.IsNullOrWhiteSpace(title) || string.Equals(title, DefaultTitle, StringComparison.OrdinalIgnoreCase);

    private static string BuildTitle(string firstMessage)
    {
        var singleLine = firstMessage.ReplaceLineEndings(" ").Trim();
        return singleLine.Length <= 48 ? singleLine : singleLine[..48].TrimEnd() + "\u2026";
    }
}
