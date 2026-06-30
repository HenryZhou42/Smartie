namespace Smartie.Application.Abstractions;

public interface IEmbeddingProvider
{
    string ProviderName { get; }

    string ModelName { get; }

    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
