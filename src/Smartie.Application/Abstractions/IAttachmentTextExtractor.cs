namespace Smartie.Application.Abstractions;

public interface IAttachmentTextExtractor
{
    bool CanExtract(string extension);

    Task<string> ExtractFromFileAsync(
        string absolutePath,
        string extension,
        CancellationToken cancellationToken = default);
}
