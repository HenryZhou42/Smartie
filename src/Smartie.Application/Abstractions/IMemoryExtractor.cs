using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IMemoryExtractor
{
    IReadOnlyList<ExtractedMemoryCandidate> Extract(string userMessage);
}

public sealed record ExtractedMemoryCandidate(
    string Content,
    MemoryCategory Category,
    MemoryImportance Importance);
