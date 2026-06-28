using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;

namespace Smartie.Application.Services;

/// <summary>
/// Coordinates AI provider selection and credentials over the repository and the
/// secret protector, and resolves the effective configuration for the chat service.
/// </summary>
public sealed class AiSettingsService : IAiSettingsService
{
    private readonly IAiSettingsRepository _repository;
    private readonly ISecretProtector _protector;
    private readonly AiOptions _options;

    public AiSettingsService(
        IAiSettingsRepository repository,
        ISecretProtector protector,
        IOptions<AiOptions> options)
    {
        _repository = repository;
        _protector = protector;
        _options = options.Value;
    }

    public async Task<AiSettingsSnapshot> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var selected = AiProviderCatalog.Normalize(
            await _repository.GetSelectedProviderAsync(userId, cancellationToken).ConfigureAwait(false));

        var credentials = (await _repository.ListCredentialsAsync(userId, cancellationToken).ConfigureAwait(false))
            .ToDictionary(c => AiProviderCatalog.Normalize(c.Provider), StringComparer.OrdinalIgnoreCase);

        var states = AiProviderCatalog.All
            .Select(info =>
            {
                credentials.TryGetValue(info.Key, out var cred);
                var hasKey = !string.IsNullOrEmpty(cred?.ApiKeyProtected);
                var model = cred?.ChatModel ?? info.DefaultChatModel;
                var endpoint = info.FixedEndpoint ?? cred?.Endpoint ?? info.DefaultEndpoint;
                return new AiProviderState(info, hasKey, model, endpoint);
            })
            .ToList();

        return new AiSettingsSnapshot(selected, states);
    }

    public async Task SetSelectedProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        var info = AiProviderCatalog.Get(provider);
        if (!info.Available)
        {
            throw new AiServiceException($"{info.DisplayName} is not available yet.");
        }

        await _repository.SetSelectedProviderAsync(userId, info.Key, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveCredentialAsync(
        Guid userId,
        string provider,
        string? apiKey,
        string? chatModel,
        string? endpoint,
        CancellationToken cancellationToken = default)
    {
        var info = AiProviderCatalog.Get(provider);

        var protectedKey = string.IsNullOrWhiteSpace(apiKey)
            ? null
            : _protector.Protect(apiKey.Trim());

        await _repository.UpsertCredentialAsync(
                userId,
                info.Key,
                protectedKey,
                NullIfBlank(chatModel),
                NullIfBlank(endpoint),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ResolvedAiProvider> ResolveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var selected = AiProviderCatalog.Normalize(
            await _repository.GetSelectedProviderAsync(userId, cancellationToken).ConfigureAwait(false));
        var info = AiProviderCatalog.GetOrDefault(selected)
                   ?? throw new AiServiceException($"AI provider '{selected}' is not supported.");

        if (!info.Available)
        {
            throw new AiServiceException($"{info.DisplayName} is not available yet. Choose another provider in Settings.");
        }

        var cred = await _repository.FindCredentialAsync(userId, info.Key, cancellationToken).ConfigureAwait(false);

        var apiKey = !string.IsNullOrEmpty(cred?.ApiKeyProtected)
            ? _protector.Unprotect(cred!.ApiKeyProtected!)
            : null;

        var chatModel = NullIfBlank(cred?.ChatModel) ?? info.DefaultChatModel;
        var endpoint = info.FixedEndpoint ?? NullIfBlank(cred?.Endpoint) ?? info.DefaultEndpoint;

        if (info.RequiresApiKey && string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiServiceException(
                $"No API key configured for {info.DisplayName}. Add one in Settings to start chatting.");
        }

        if (info.RequiresEndpoint && string.IsNullOrWhiteSpace(endpoint))
        {
            throw new AiServiceException(
                $"No endpoint configured for {info.DisplayName}. Set it in Settings.");
        }

        return new ResolvedAiProvider(info.Key, chatModel, apiKey, endpoint, _options.SystemPrompt);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
