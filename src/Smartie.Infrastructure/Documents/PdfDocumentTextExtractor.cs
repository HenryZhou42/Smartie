using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Documents;

public sealed class PdfDocumentTextExtractor : IDocumentTextExtractor
{
    public const string ExtractorName = "PdfDocumentTextExtractor";

    private readonly IDocumentStorage _storage;

    public PdfDocumentTextExtractor(IDocumentStorage storage)
    {
        _storage = storage;
    }

    public bool CanExtract(Document document) =>
        DocumentExtensionMatcher.IsAny(document.Extension, ".pdf", "pdf");

    public Task<string> ExtractTextAsync(Document document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = _storage.GetAbsolutePath(document.RelativePath);
        return Task.FromResult(FilePathTextExtractor.ExtractPdfText(path));
    }
}
