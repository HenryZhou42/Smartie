using Smartie.Application.Abstractions;
using Smartie.Infrastructure.Storage;

namespace Smartie.Infrastructure.Storage;

public sealed class LocalChatAttachmentStorage : IChatAttachmentStorage
{
    public async Task<ChatAttachmentFileInfo> SaveStagingAsync(
        Guid conversationId,
        Guid stagingId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var safeName = Path.GetFileName(originalFileName);
        var directory = SmartiePaths.GetChatAttachmentDirectory(conversationId, stagingId);
        var absolutePath = Path.Combine(directory, safeName);
        var relativePath = Path.Combine(conversationId.ToString("N"), stagingId.ToString("N"), safeName)
            .Replace('\\', '/');

        await using var fileStream = new FileStream(
            absolutePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

        var extension = Path.GetExtension(safeName).TrimStart('.').ToLowerInvariant();
        var sizeBytes = new FileInfo(absolutePath).Length;

        return new ChatAttachmentFileInfo(
            safeName,
            safeName,
            relativePath,
            extension,
            sizeBytes);
    }

    public Task<IReadOnlyList<ChatAttachmentFileInfo>> CommitStagingAsync(
        Guid conversationId,
        Guid stagingId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var stagingDirectory = SmartiePaths.GetChatAttachmentDirectory(conversationId, stagingId);
        if (!Directory.Exists(stagingDirectory))
        {
            throw new DirectoryNotFoundException($"Staging attachment {stagingId} was not found.");
        }

        var messageDirectory = SmartiePaths.GetChatAttachmentDirectory(conversationId, messageId);
        var results = new List<ChatAttachmentFileInfo>();

        foreach (var file in Directory.GetFiles(stagingDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(file);
            var destination = Path.Combine(messageDirectory, fileName);
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            File.Move(file, destination);

            var relativePath = Path.Combine(conversationId.ToString("N"), messageId.ToString("N"), fileName)
                .Replace('\\', '/');
            var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
            var sizeBytes = new FileInfo(destination).Length;

            results.Add(new ChatAttachmentFileInfo(
                fileName,
                fileName,
                relativePath,
                extension,
                sizeBytes));
        }

        try
        {
            if (Directory.Exists(stagingDirectory) && !Directory.EnumerateFileSystemEntries(stagingDirectory).Any())
            {
                Directory.Delete(stagingDirectory);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }

        return Task.FromResult<IReadOnlyList<ChatAttachmentFileInfo>>(results);
    }

    public Task DeleteStagingAsync(
        Guid conversationId,
        Guid stagingId,
        CancellationToken cancellationToken = default)
    {
        var stagingDirectory = SmartiePaths.GetChatAttachmentDirectory(conversationId, stagingId);
        if (Directory.Exists(stagingDirectory))
        {
            Directory.Delete(stagingDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    public string GetAbsolutePath(string relativePath) =>
        SmartiePaths.GetChatAttachmentAbsolutePath(relativePath);

    public bool StagingExists(Guid conversationId, Guid stagingId) =>
        Directory.Exists(SmartiePaths.GetChatAttachmentDirectory(conversationId, stagingId))
        && Directory.EnumerateFiles(SmartiePaths.GetChatAttachmentDirectory(conversationId, stagingId)).Any();
}
