using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

/// <summary>
/// Persistence for per-user AI provider selection and credentials.
/// </summary>
public interface IAiSettingsRepository
{
    Task<string> GetSelectedProviderAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SetSelectedProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiProviderCredential>> ListCredentialsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AiProviderCredential?> FindCredentialAsync(Guid userId, string provider, CancellationToken cancellationToken = default);

    Task UpsertCredentialAsync(
        Guid userId,
        string provider,
        string? apiKeyProtected,
        string? chatModel,
        string? endpoint,
        CancellationToken cancellationToken = default);
}
