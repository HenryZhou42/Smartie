namespace Smartie.Contracts;

/// <summary>A single AI provider's state for the Settings UI (never includes the key).</summary>
public sealed record AiProviderDto(
    string Provider,
    string DisplayName,
    bool Available,
    bool RequiresApiKey,
    bool RequiresEndpoint,
    bool HasApiKey,
    string ChatModel,
    string? Endpoint,
    string DefaultChatModel,
    string? DefaultEndpoint);

/// <summary>A user's AI settings: which provider is active and the per-provider state.</summary>
public sealed record AiSettingsDto(
    string SelectedProvider,
    IReadOnlyList<AiProviderDto> Providers);

/// <summary>Select the active AI provider.</summary>
public sealed record SelectProviderRequest(string Provider);

/// <summary>
/// Save a provider's settings. A null/empty <see cref="ApiKey"/> leaves any stored
/// key unchanged so the UI never has to round-trip the secret.
/// </summary>
public sealed record SaveProviderCredentialRequest(
    string? ApiKey,
    string? ChatModel,
    string? Endpoint);
