using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;

namespace Smartie.Infrastructure.Ai;

public sealed class GeminiEmbeddingProvider : IEmbeddingProvider
{
    private readonly ITextEmbeddingGenerationService _service;

    public GeminiEmbeddingProvider(string modelName, string apiKey)
    {
        ModelName = modelName;
        ProviderName = AiProviderCatalog.Google;

        var builder = Kernel.CreateBuilder();
        builder.AddGoogleAIEmbeddingGeneration(modelName, apiKey);
        _service = builder.Build().GetRequiredService<ITextEmbeddingGenerationService>();
    }

    public string ProviderName { get; }

    public string ModelName { get; }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embedding = await _service
            .GenerateEmbeddingAsync(text, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return embedding.ToArray();
    }
}
