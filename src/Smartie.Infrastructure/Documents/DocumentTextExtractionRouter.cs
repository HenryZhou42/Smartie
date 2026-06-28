using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Documents;

public sealed class DocumentTextExtractionRouter : IDocumentTextExtractionRouter
{
    private readonly IReadOnlyList<IDocumentTextExtractor> _extractors;

    public DocumentTextExtractionRouter(
        TxtDocumentTextExtractor txt,
        MarkdownDocumentTextExtractor markdown,
        PdfDocumentTextExtractor pdf,
        DocxDocumentTextExtractor docx)
    {
        _extractors = [txt, markdown, pdf, docx];
    }

    public string? GetExtractorName(Document document) =>
        _extractors.FirstOrDefault(e => e.CanExtract(document)) switch
        {
            TxtDocumentTextExtractor => TxtDocumentTextExtractor.ExtractorName,
            MarkdownDocumentTextExtractor => MarkdownDocumentTextExtractor.ExtractorName,
            PdfDocumentTextExtractor => PdfDocumentTextExtractor.ExtractorName,
            DocxDocumentTextExtractor => DocxDocumentTextExtractor.ExtractorName,
            _ => null
        };

    public async Task<string> ExtractTextAsync(Document document, CancellationToken cancellationToken = default)
    {
        var extractor = _extractors.FirstOrDefault(e => e.CanExtract(document))
            ?? throw new NotSupportedException(
                $"Text extraction is not supported for files with extension '{document.Extension}'.");

        return await extractor.ExtractTextAsync(document, cancellationToken).ConfigureAwait(false);
    }
}
