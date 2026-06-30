namespace Smartie.Application.Abstractions;

public interface IMemoryPromptBuilder
{
    Task<(string? PromptBlock, MemoryRetrievalDiagnostics Diagnostics)> BuildMemoryContextAsync(
        Guid userId,
        string query,
        CancellationToken cancellationToken = default);
}
