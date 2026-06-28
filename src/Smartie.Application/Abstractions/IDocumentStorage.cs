namespace Smartie.Application.Abstractions;

public interface IDocumentStorage
{
    Task<string> SaveAsync(Guid documentId, string fileName, Stream content, CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    string GetAbsolutePath(string relativePath);

    bool Exists(string relativePath);

    string GetStorageRoot();
}
