namespace Smartie.Application.Abstractions;

public sealed record StagedChatAttachment(
    Guid StagingId,
    string FileName,
    string Extension,
    string TypeLabel,
    long SizeBytes);

public interface IChatAttachmentService
{
    Task<StagedChatAttachment> StageUploadAsync(
        Guid userId,
        Guid conversationId,
        string originalFileName,
        Stream content,
        long sizeBytes,
        CancellationToken cancellationToken = default);

    Task DeleteStagingAsync(
        Guid userId,
        Guid conversationId,
        Guid stagingId,
        CancellationToken cancellationToken = default);
}
