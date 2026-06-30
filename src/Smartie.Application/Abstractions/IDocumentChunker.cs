using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IDocumentChunker
{
    Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
        Document document,
        string extractedText,
        CancellationToken cancellationToken = default);
}
