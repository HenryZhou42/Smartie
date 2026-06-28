using Smartie.Application.Abstractions;
using Smartie.Infrastructure.Storage;

namespace Smartie.Infrastructure.Storage;

public sealed class LocalDocumentStorage : IDocumentStorage
{
    public async Task<string> SaveAsync(
        Guid documentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var directory = SmartiePaths.GetDocumentDirectory(documentId);
        var safeName = Path.GetFileName(fileName);
        var absolutePath = Path.Combine(directory, safeName);
        var relativePath = Path.Combine(documentId.ToString("N"), safeName).Replace('\\', '/');

        await using var fileStream = new FileStream(
            absolutePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        return relativePath;
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = GetAbsolutePath(relativePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        var directory = Path.GetDirectoryName(absolutePath);
        if (directory is not null && Directory.Exists(directory))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        return Task.CompletedTask;
    }

    public string GetAbsolutePath(string relativePath) => SmartiePaths.GetAbsolutePath(relativePath);

    public bool Exists(string relativePath) => File.Exists(GetAbsolutePath(relativePath));

    public string GetStorageRoot() => SmartiePaths.DocumentsRoot;
}
