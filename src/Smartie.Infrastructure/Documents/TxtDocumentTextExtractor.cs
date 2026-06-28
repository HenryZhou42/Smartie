using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Documents;

public sealed class TxtDocumentTextExtractor : IDocumentTextExtractor
{
    public const string ExtractorName = "TxtDocumentTextExtractor";

    private readonly IDocumentStorage _storage;

    public TxtDocumentTextExtractor(IDocumentStorage storage)
    {
        _storage = storage;
    }

    public bool CanExtract(Document document) =>
        DocumentExtensionMatcher.IsAny(document.Extension, ".txt", "txt");

    public Task<string> ExtractTextAsync(Document document, CancellationToken cancellationToken = default)
    {
        var path = _storage.GetAbsolutePath(document.RelativePath);
        return FilePathTextExtractor.ReadTextFileAsync(path, cancellationToken);
    }
}
