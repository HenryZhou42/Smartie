using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;

namespace Smartie.Infrastructure.Ai;

public sealed class EmbeddingProviderFactory : IEmbeddingProviderFactory
{
    private readonly ConcurrentDictionary<string, IEmbeddingProvider> _cache = new();

    public IEmbeddingProvider Create(ResolvedEmbeddingProvider settings)
    {
        var cacheKey = string.Join('|', settings.Provider, settings.EmbeddingModel, ShortHash(settings.ApiKey));
        return _cache.GetOrAdd(cacheKey, _ => Build(settings));
    }

    private static IEmbeddingProvider Build(ResolvedEmbeddingProvider settings) =>
        settings.Provider switch
        {
            AiProviderCatalog.Google => new GeminiEmbeddingProvider(settings.EmbeddingModel, settings.ApiKey),
            _ => throw new AiServiceException($"Embedding provider '{settings.Provider}' is not supported.")
        };

    private static string ShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes, 0, 6);
    }
}
