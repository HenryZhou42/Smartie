namespace Smartie.Application.Configuration;

/// <summary>
/// Static metadata for an AI provider Smartie can talk to. Drives the Settings UI
/// and the connector that gets built at request time.
/// </summary>
public sealed record AiProviderInfo(
    string Key,
    string DisplayName,
    bool RequiresApiKey,
    bool RequiresEndpoint,
    string DefaultChatModel,
    string? DefaultEndpoint,
    string? FixedEndpoint,
    bool Available);

/// <summary>
/// The set of providers Smartie understands. Providers are addressed by a lowercase
/// <see cref="AiProviderInfo.Key"/>; selection is persisted per user.
/// </summary>
public static class AiProviderCatalog
{
    public const string Google = "google";
    public const string OpenAI = "openai";
    public const string OpenRouter = "openrouter";
    public const string Ollama = "ollama";
    public const string SmartieCloud = "smartiecloud";

    private static readonly IReadOnlyDictionary<string, AiProviderInfo> Providers =
        new Dictionary<string, AiProviderInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [Google] = new(
                Google, "Google Gemini",
                RequiresApiKey: true, RequiresEndpoint: false,
                DefaultChatModel: "gemini-2.5-flash",
                DefaultEndpoint: null, FixedEndpoint: null, Available: true),

            [OpenAI] = new(
                OpenAI, "OpenAI",
                RequiresApiKey: true, RequiresEndpoint: false,
                DefaultChatModel: "gpt-4o-mini",
                DefaultEndpoint: null, FixedEndpoint: null, Available: true),

            [OpenRouter] = new(
                OpenRouter, "OpenRouter",
                RequiresApiKey: true, RequiresEndpoint: false,
                DefaultChatModel: "openai/gpt-4o-mini",
                DefaultEndpoint: null, FixedEndpoint: "https://openrouter.ai/api/v1", Available: true),

            [Ollama] = new(
                Ollama, "Ollama (local)",
                RequiresApiKey: false, RequiresEndpoint: true,
                DefaultChatModel: "llama3.1",
                DefaultEndpoint: "http://localhost:11434/v1", FixedEndpoint: null, Available: true),

            // Reserved for a future hosted edition — not part of Community Edition.
            [SmartieCloud] = new(
                SmartieCloud, "Smartie Cloud",
                RequiresApiKey: false, RequiresEndpoint: false,
                DefaultChatModel: "smartie-default",
                DefaultEndpoint: null, FixedEndpoint: null, Available: false),
        };

    public static IReadOnlyCollection<AiProviderInfo> All => Providers.Values.ToArray();

    public static string Normalize(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ? Google : provider.Trim().ToLowerInvariant();

    public static bool IsKnown(string? provider) =>
        provider is not null && Providers.ContainsKey(Normalize(provider));

    public static AiProviderInfo Get(string provider)
    {
        var key = Normalize(provider);
        return Providers.TryGetValue(key, out var info)
            ? info
            : throw new ArgumentOutOfRangeException(nameof(provider), $"Unknown AI provider '{provider}'.");
    }

    public static AiProviderInfo? GetOrDefault(string? provider) =>
        provider is not null && Providers.TryGetValue(Normalize(provider), out var info) ? info : null;
}
