using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Documents;

public sealed class MarkdownDocumentTextExtractor : IDocumentTextExtractor
{
    public const string ExtractorName = "MarkdownDocumentTextExtractor";

    private readonly IDocumentStorage _storage;

    public MarkdownDocumentTextExtractor(IDocumentStorage storage)
    {
        _storage = storage;
    }

    public bool CanExtract(Document document) =>
        DocumentExtensionMatcher.IsAny(document.Extension, ".md", "md", ".markdown", "markdown");

    public Task<string> ExtractTextAsync(Document document, CancellationToken cancellationToken = default)
    {
        var path = _storage.GetAbsolutePath(document.RelativePath);
        return FilePathTextExtractor.ReadTextFileAsync(path, cancellationToken);
    }
}
