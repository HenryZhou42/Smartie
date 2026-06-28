namespace Smartie.Domain.Entities;

/// <summary>
/// A user's stored configuration for a single AI provider (Community / "Bring Your
/// Own AI" edition). The API key is stored encrypted at rest; this entity never
/// holds the plaintext key.
/// </summary>
public class AiProviderCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>Lowercase provider key (see AiProviderCatalog), e.g. "openai".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Encrypted (DPAPI) API key blob, base64-encoded. Null when not set.</summary>
    public string? ApiKeyProtected { get; set; }

    public string? ChatModel { get; set; }

    public string? Endpoint { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}
