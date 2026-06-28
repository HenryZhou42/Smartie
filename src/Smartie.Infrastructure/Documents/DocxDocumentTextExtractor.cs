using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Documents;

public sealed class DocxDocumentTextExtractor : IDocumentTextExtractor
{
    public const string ExtractorName = "DocxDocumentTextExtractor";

    private readonly IDocumentStorage _storage;

    public DocxDocumentTextExtractor(IDocumentStorage storage)
    {
        _storage = storage;
    }

    public bool CanExtract(Document document) =>
        DocumentExtensionMatcher.IsAny(document.Extension, ".docx", "docx");

    public Task<string> ExtractTextAsync(Document document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = _storage.GetAbsolutePath(document.RelativePath);
        return Task.FromResult(FilePathTextExtractor.ExtractDocxText(path));
    }
}
