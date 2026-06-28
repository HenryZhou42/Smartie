using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IDocumentExtractionService
{
    Task<Document> ExtractAndPersistAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
