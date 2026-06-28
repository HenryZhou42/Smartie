using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;

namespace Smartie.Infrastructure.Ai;

/// <summary>
/// Builds (and caches) a Semantic Kernel <see cref="IChatCompletionService"/> for a
/// resolved provider configuration. OpenAI, OpenRouter and Ollama all speak the
/// OpenAI wire format, so they share the OpenAI connector with a different endpoint.
/// </summary>
public interface IChatCompletionProvider
{
    IChatCompletionService Get(ResolvedAiProvider settings);
}

public sealed class ChatCompletionProviderFactory : IChatCompletionProvider
{
    private readonly ConcurrentDictionary<string, IChatCompletionService> _cache = new();

    public IChatCompletionService Get(ResolvedAiProvider settings)
    {
        var cacheKey = string.Join(
            '|',
            settings.Provider,
            settings.ChatModel,
            settings.Endpoint ?? string.Empty,
            ShortHash(settings.ApiKey));

        return _cache.GetOrAdd(cacheKey, _ => Build(settings));
    }

    private static IChatCompletionService Build(ResolvedAiProvider s)
    {
        var builder = Kernel.CreateBuilder();

        switch (s.Provider)
        {
            case AiProviderCatalog.Google:
                builder.AddGoogleAIGeminiChatCompletion(s.ChatModel, s.ApiKey!);
                break;

            case AiProviderCatalog.OpenAI:
                builder.AddOpenAIChatCompletion(s.ChatModel, s.ApiKey!);
                break;

            case AiProviderCatalog.OpenRouter:
                builder.AddOpenAIChatCompletion(s.ChatModel, new Uri(s.Endpoint!), s.ApiKey!);
                break;

            case AiProviderCatalog.Ollama:
                // Ollama's OpenAI-compatible endpoint ignores the key, but the connector requires one.
                builder.AddOpenAIChatCompletion(s.ChatModel, new Uri(s.Endpoint!), s.ApiKey ?? "ollama");
                break;

            default:
                throw new AiServiceException($"AI provider '{s.Provider}' is not supported.");
        }

        return builder.Build().GetRequiredService<IChatCompletionService>();
    }

    private static string ShortHash(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "none";
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes, 0, 6);
    }
}
