using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;

namespace Smartie.Application.Services;

public sealed class ChatAttachmentService : IChatAttachmentService
{
    private readonly IConversationRepository _conversations;
    private readonly IChatAttachmentStorage _storage;
    private readonly KnowledgeBaseOptions _options;

    public ChatAttachmentService(
        IConversationRepository conversations,
        IChatAttachmentStorage storage,
        IOptions<KnowledgeBaseOptions> options)
    {
        _conversations = conversations;
        _storage = storage;
        _options = options.Value;
    }

    public async Task<StagedChatAttachment> StageUploadAsync(
        Guid userId,
        Guid conversationId,
        string originalFileName,
        Stream content,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        await EnsureConversationAsync(userId, conversationId, cancellationToken).ConfigureAwait(false);

        var extension = Path.GetExtension(originalFileName);
        if (!IsAllowedExtension(extension))
        {
            throw new InvalidOperationException(
                $"File type '{extension}' is not supported. Allowed: PDF, DOCX, TXT, Markdown.");
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentException("File is empty.", nameof(sizeBytes));
        }

        if (sizeBytes > _options.MaxFileSizeBytes)
        {
            throw new InvalidOperationException("File exceeds the maximum allowed size.");
        }

        var stagingId = Guid.NewGuid();
        var saved = await _storage
            .SaveStagingAsync(conversationId, stagingId, originalFileName, content, cancellationToken)
            .ConfigureAwait(false);

        return new StagedChatAttachment(
            stagingId,
            saved.OriginalFileName,
            saved.Extension,
            GetTypeLabel(saved.Extension),
            saved.SizeBytes);
    }

    public async Task DeleteStagingAsync(
        Guid userId,
        Guid conversationId,
        Guid stagingId,
        CancellationToken cancellationToken = default)
    {
        await EnsureConversationAsync(userId, conversationId, cancellationToken).ConfigureAwait(false);
        await _storage.DeleteStagingAsync(conversationId, stagingId, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureConversationAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await _conversations.FindAsync(conversationId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Conversation {conversationId} was not found.");

        if (conversation.UserId != userId)
        {
            throw new UnauthorizedAccessException("Conversation access denied.");
        }
    }

    private bool IsAllowedExtension(string extension) =>
        _options.AllowedExtensions.Any(e =>
            string.Equals(e, extension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.TrimStart('.'), extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase));

    private static string GetTypeLabel(string extension) =>
        extension.ToLowerInvariant() switch
        {
            "pdf" => "PDF",
            "docx" => "DOCX",
            "txt" => "TXT",
            "md" or "markdown" => "Markdown",
            _ => extension.ToUpperInvariant()
        };
}
