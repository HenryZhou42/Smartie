namespace Smartie.Application.Abstractions;

public interface IEmbeddingProviderFactory
{
    IEmbeddingProvider Create(ResolvedEmbeddingProvider settings);
}
