using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IDocumentTextExtractionRouter
{
    string? GetExtractorName(Document document);

    Task<string> ExtractTextAsync(Document document, CancellationToken cancellationToken = default);
}
