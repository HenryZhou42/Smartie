namespace Smartie.Application.Abstractions;

/// <summary>Resolved Google Gemini settings used to generate document embeddings.</summary>
public sealed record ResolvedEmbeddingProvider(
    string Provider,
    string EmbeddingModel,
    string ApiKey);
