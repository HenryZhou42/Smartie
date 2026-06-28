using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IMessageAttachmentRepository
{
    Task AddKnowledgeBaseAttachmentsAsync(
        Guid messageId,
        Guid conversationId,
        IReadOnlyList<Guid> documentIds,
        CancellationToken cancellationToken = default);

    Task AddLocalUploadAttachmentsAsync(
        Guid messageId,
        Guid conversationId,
        IReadOnlyList<MessageAttachment> attachments,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageAttachment>> GetForMessageAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);
}
