namespace Smartie.Application.Configuration;

/// <summary>
/// AI configuration bound from the "Ai" section. Community Edition stores
/// provider selection and keys in SQLite; this section only supplies shared
/// options such as the system prompt.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Legacy default; provider is selected per user in Settings.</summary>
    public string Provider { get; set; } = "Google";

    public string SystemPrompt { get; set; } =
        "You are Smartie, an AI-powered productivity assistant. Be clear, concise and helpful. " +
        "Use Markdown for formatting when it improves readability.";

    public GoogleAiOptions Google { get; set; } = new();

    public OpenAiOptions OpenAI { get; set; } = new();

    public OllamaOptions Ollama { get; set; } = new();
}

public sealed class GoogleAiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ChatModel { get; set; } = "gemini-2.5-flash";
    public string EmbeddingModel { get; set; } = "text-embedding-004";
}

public sealed class OpenAiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}

public sealed class OllamaOptions
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string ChatModel { get; set; } = "llama3.1";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}
