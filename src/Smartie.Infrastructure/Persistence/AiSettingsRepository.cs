using Microsoft.EntityFrameworkCore;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IAiSettingsRepository"/>.
/// </summary>
public sealed class AiSettingsRepository : IAiSettingsRepository
{
    private readonly SmartieDbContext _db;

    public AiSettingsRepository(SmartieDbContext db)
    {
        _db = db;
    }

    public async Task<string> GetSelectedProviderAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var selected = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.SelectedAiProvider)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return AiProviderCatalog.Normalize(selected);
    }

    public async Task SetSelectedProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        user.SelectedAiProvider = AiProviderCatalog.Normalize(provider);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AiProviderCredential>> ListCredentialsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _db.AiProviderCredentials
            .Where(c => c.UserId == userId)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<AiProviderCredential?> FindCredentialAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var key = AiProviderCatalog.Normalize(provider);
        return await _db.AiProviderCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == key, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpsertCredentialAsync(
        Guid userId,
        string provider,
        string? apiKeyProtected,
        string? chatModel,
        string? endpoint,
        CancellationToken cancellationToken = default)
    {
        var key = AiProviderCatalog.Normalize(provider);
        var existing = await _db.AiProviderCredentials
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == key, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _db.AiProviderCredentials.Add(new AiProviderCredential
            {
                UserId = userId,
                Provider = key,
                ApiKeyProtected = apiKeyProtected,
                ChatModel = chatModel,
                Endpoint = endpoint,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            // A null incoming key means "leave the stored key unchanged".
            if (apiKeyProtected is not null)
            {
                existing.ApiKeyProtected = apiKeyProtected;
            }

            existing.ChatModel = chatModel;
            existing.Endpoint = endpoint;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
