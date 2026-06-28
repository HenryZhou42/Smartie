using Microsoft.EntityFrameworkCore;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Persistence;

public sealed class MessageAttachmentRepository : IMessageAttachmentRepository
{
    private readonly SmartieDbContext _db;

    public MessageAttachmentRepository(SmartieDbContext db)
    {
        _db = db;
    }

    public async Task AddKnowledgeBaseAttachmentsAsync(
        Guid messageId,
        Guid conversationId,
        IReadOnlyList<Guid> documentIds,
        CancellationToken cancellationToken = default)
    {
        if (documentIds.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var documentId in documentIds.Distinct())
        {
            var document = await _db.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Document {documentId} was not found.");

            _db.MessageAttachments.Add(new MessageAttachment
            {
                MessageId = messageId,
                ConversationId = conversationId,
                DocumentId = documentId,
                OriginalFileName = document.FileName,
                StoredFileName = document.FileName,
                FilePath = document.RelativePath,
                Extension = document.Extension,
                SizeBytes = document.SizeBytes,
                SourceType = MessageAttachmentSourceType.KnowledgeBase,
                CreatedAt = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddLocalUploadAttachmentsAsync(
        Guid messageId,
        Guid conversationId,
        IReadOnlyList<MessageAttachment> attachments,
        CancellationToken cancellationToken = default)
    {
        if (attachments.Count == 0)
        {
            return;
        }

        foreach (var attachment in attachments)
        {
            attachment.MessageId = messageId;
            attachment.ConversationId = conversationId;
            attachment.SourceType = MessageAttachmentSourceType.LocalUpload;
            _db.MessageAttachments.Add(attachment);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MessageAttachment>> GetForMessageAsync(
        Guid messageId,
        CancellationToken cancellationToken = default) =>
        await _db.MessageAttachments
            .AsNoTracking()
            .Include(a => a.Document)
            .Where(a => a.MessageId == messageId)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
