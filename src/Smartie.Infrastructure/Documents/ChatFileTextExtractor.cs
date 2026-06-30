using Smartie.Application.Abstractions;
using Smartie.Infrastructure.Documents;

namespace Smartie.Infrastructure.Documents;

public sealed class ChatFileTextExtractor : IAttachmentTextExtractor
{
    public bool CanExtract(string extension) =>
        DocumentExtensionMatcher.IsAny(extension, ".txt", "txt", ".md", "md", ".markdown", "markdown", ".pdf", "pdf", ".docx", "docx", ".csv", "csv");

    public async Task<string> ExtractFromFileAsync(
        string absolutePath,
        string extension,
        CancellationToken cancellationToken = default)
    {
        if (DocumentExtensionMatcher.IsAny(extension, ".txt", "txt", ".md", "md", ".markdown", "markdown", ".csv", "csv"))
        {
            return await FilePathTextExtractor.ReadTextFileAsync(absolutePath, cancellationToken).ConfigureAwait(false);
        }

        if (DocumentExtensionMatcher.IsAny(extension, ".pdf", "pdf"))
        {
            return FilePathTextExtractor.ExtractPdfText(absolutePath);
        }

        if (DocumentExtensionMatcher.IsAny(extension, ".docx", "docx"))
        {
            return FilePathTextExtractor.ExtractDocxText(absolutePath);
        }

        throw new NotSupportedException(
            $"Text extraction is not supported for files with extension '{extension}'.");
    }
}
