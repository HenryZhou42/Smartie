using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IDocumentTextExtractor
{
    bool CanExtract(Document document);

    Task<string> ExtractTextAsync(Document document, CancellationToken cancellationToken = default);
}
