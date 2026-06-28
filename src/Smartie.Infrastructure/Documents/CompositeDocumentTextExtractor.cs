using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Documents;

/// <summary>
/// Routes extraction to the first registered extractor that supports the document type.
/// </summary>
public sealed class CompositeDocumentTextExtractor : IDocumentTextExtractor
{
    private readonly IReadOnlyList<IDocumentTextExtractor> _extractors;

    public CompositeDocumentTextExtractor(IEnumerable<IDocumentTextExtractor> extractors)
    {
        _extractors = extractors.ToList();
    }

    public bool CanExtract(Document document) =>
        _extractors.Any(e => e.CanExtract(document));

    public async Task<string> ExtractTextAsync(Document document, CancellationToken cancellationToken = default)
    {
        var extractor = _extractors.FirstOrDefault(e => e.CanExtract(document));
        if (extractor is null)
        {
            throw new NotSupportedException(
                $"Text extraction is not supported for files with extension '{document.Extension}'.");
        }

        return await extractor.ExtractTextAsync(document, cancellationToken).ConfigureAwait(false);
    }
}
