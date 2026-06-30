using Smartie.Application.Configuration;

namespace Smartie.Application.Abstractions;

/// <summary>
/// A provider as presented to the settings UI. Never contains the raw API key —
/// only whether one is stored (<see cref="HasApiKey"/>).
/// </summary>
public sealed record AiProviderState(
    AiProviderInfo Info,
    bool HasApiKey,
    string? ChatModel,
    string? Endpoint);

/// <summary>Snapshot of a user's AI settings for display/editing.</summary>
public sealed record AiSettingsSnapshot(
    string SelectedProvider,
    IReadOnlyList<AiProviderState> Providers);

/// <summary>The fully-resolved, ready-to-use settings for the active provider.</summary>
public sealed record ResolvedAiProvider(
    string Provider,
    string ChatModel,
    string? ApiKey,
    string? Endpoint,
    string SystemPrompt);

/// <summary>
/// Application service for reading/updating AI provider settings and resolving the
/// effective provider configuration used to talk to the model.
/// </summary>
public interface IAiSettingsService
{
    Task<AiSettingsSnapshot> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SetSelectedProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves model/endpoint for a provider and, when <paramref name="apiKey"/> is
    /// non-empty, encrypts and stores it. A null/empty key leaves any existing key intact.
    /// </summary>
    Task SaveCredentialAsync(
        Guid userId,
        string provider,
        string? apiKey,
        string? chatModel,
        string? endpoint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the active provider's effective settings (with decrypted key) for use
    /// by the chat service. Throws <see cref="AiServiceException"/> when the selected
    /// provider is unavailable or missing required configuration.
    /// </summary>
    Task<ResolvedAiProvider> ResolveAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves Google Gemini settings for document embeddings. Requires a stored Google API key.
    /// </summary>
    Task<ResolvedEmbeddingProvider> ResolveEmbeddingAsync(Guid userId, CancellationToken cancellationToken = default);
}
