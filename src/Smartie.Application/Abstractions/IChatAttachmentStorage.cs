namespace Smartie.Application.Abstractions;

public interface IChatAttachmentStorage
{
    Task<ChatAttachmentFileInfo> SaveStagingAsync(
        Guid conversationId,
        Guid stagingId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatAttachmentFileInfo>> CommitStagingAsync(
        Guid conversationId,
        Guid stagingId,
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task DeleteStagingAsync(
        Guid conversationId,
        Guid stagingId,
        CancellationToken cancellationToken = default);

    string GetAbsolutePath(string relativePath);

    bool StagingExists(Guid conversationId, Guid stagingId);
}

public sealed record ChatAttachmentFileInfo(
    string OriginalFileName,
    string StoredFileName,
    string RelativePath,
    string Extension,
    long SizeBytes);
