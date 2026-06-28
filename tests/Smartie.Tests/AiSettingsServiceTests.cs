using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Application.Services;
using Smartie.Domain.Entities;

namespace Smartie.Tests;

public class AiSettingsServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static AiSettingsService CreateService(
        out InMemoryAiSettingsRepository repo,
        string? googleAppSettingsKey = null)
    {
        repo = new InMemoryAiSettingsRepository();
        var options = Options.Create(new AiOptions
        {
            SystemPrompt = "test-prompt",
            Google = new GoogleAiOptions { ApiKey = googleAppSettingsKey ?? string.Empty }
        });
        return new AiSettingsService(repo, new Base64SecretProtector(), options);
    }

    [Fact]
    public async Task ResolveAsync_Throws_WhenSelectedProviderHasNoKey()
    {
        var service = CreateService(out _);
        await service.SetSelectedProviderAsync(UserId, AiProviderCatalog.OpenAI);

        var ex = await Assert.ThrowsAsync<AiServiceException>(() => service.ResolveAsync(UserId));
        Assert.Contains("API key", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveCredential_ThenResolve_ReturnsDecryptedKeyAndModel()
    {
        var service = CreateService(out _);
        await service.SaveCredentialAsync(UserId, AiProviderCatalog.OpenAI, "sk-test-123", "gpt-4o", null);
        await service.SetSelectedProviderAsync(UserId, AiProviderCatalog.OpenAI);

        var resolved = await service.ResolveAsync(UserId);

        Assert.Equal(AiProviderCatalog.OpenAI, resolved.Provider);
        Assert.Equal("sk-test-123", resolved.ApiKey);
        Assert.Equal("gpt-4o", resolved.ChatModel);
        Assert.Equal("test-prompt", resolved.SystemPrompt);
    }

    [Fact]
    public async Task ResolveAsync_Google_RequiresUserStoredKey()
    {
        var service = CreateService(out _);
        // google is the default selected provider; no key saved

        var ex = await Assert.ThrowsAsync<AiServiceException>(() => service.ResolveAsync(UserId));
        Assert.Contains("API key", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveCredential_WithNullKey_KeepsExistingKey()
    {
        var service = CreateService(out _);
        await service.SaveCredentialAsync(UserId, AiProviderCatalog.OpenAI, "sk-first", "gpt-4o", null);
        await service.SaveCredentialAsync(UserId, AiProviderCatalog.OpenAI, null, "gpt-4o-mini", null);
        await service.SetSelectedProviderAsync(UserId, AiProviderCatalog.OpenAI);

        var resolved = await service.ResolveAsync(UserId);

        Assert.Equal("sk-first", resolved.ApiKey);
        Assert.Equal("gpt-4o-mini", resolved.ChatModel);
    }

    [Fact]
    public async Task ResolveAsync_Ollama_UsesFixedDefaultsWithoutKey()
    {
        var service = CreateService(out _);
        await service.SetSelectedProviderAsync(UserId, AiProviderCatalog.Ollama);

        var resolved = await service.ResolveAsync(UserId);

        Assert.Equal(AiProviderCatalog.Ollama, resolved.Provider);
        Assert.Equal("http://localhost:11434/v1", resolved.Endpoint);
        Assert.Equal("llama3.1", resolved.ChatModel);
    }

    [Fact]
    public async Task SetSelectedProvider_Throws_ForUnavailableProvider()
    {
        var service = CreateService(out _);
        await Assert.ThrowsAsync<AiServiceException>(
            () => service.SetSelectedProviderAsync(UserId, AiProviderCatalog.SmartieCloud));
    }

    [Fact]
    public async Task GetSnapshot_ReportsStoredKeyAndSelection()
    {
        var service = CreateService(out _);
        await service.SaveCredentialAsync(UserId, AiProviderCatalog.OpenAI, "sk-test", null, null);
        await service.SetSelectedProviderAsync(UserId, AiProviderCatalog.OpenAI);

        var snapshot = await service.GetSnapshotAsync(UserId);

        Assert.Equal(AiProviderCatalog.OpenAI, snapshot.SelectedProvider);
        var openai = snapshot.Providers.Single(p => p.Info.Key == AiProviderCatalog.OpenAI);
        Assert.True(openai.HasApiKey);
    }
}

/// <summary>A base64 stand-in for the DPAPI protector (tests run cross-process/CI-safe).</summary>
internal sealed class Base64SecretProtector : ISecretProtector
{
    public string Protect(string plaintext) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));

    public string? Unprotect(string protectedValue) =>
        System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue));
}

internal sealed class InMemoryAiSettingsRepository : IAiSettingsRepository
{
    private readonly Dictionary<Guid, string> _selected = new();
    private readonly Dictionary<(Guid, string), AiProviderCredential> _creds = new();

    public Task<string> GetSelectedProviderAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(AiProviderCatalog.Normalize(_selected.GetValueOrDefault(userId)));

    public Task SetSelectedProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        _selected[userId] = AiProviderCatalog.Normalize(provider);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AiProviderCredential>> ListCredentialsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiProviderCredential>>(
            _creds.Where(kv => kv.Key.Item1 == userId).Select(kv => kv.Value).ToList());

    public Task<AiProviderCredential?> FindCredentialAsync(Guid userId, string provider, CancellationToken cancellationToken = default) =>
        Task.FromResult(_creds.GetValueOrDefault((userId, AiProviderCatalog.Normalize(provider))));

    public Task UpsertCredentialAsync(
        Guid userId,
        string provider,
        string? apiKeyProtected,
        string? chatModel,
        string? endpoint,
        CancellationToken cancellationToken = default)
    {
        var key = AiProviderCatalog.Normalize(provider);
        if (_creds.TryGetValue((userId, key), out var existing))
        {
            if (apiKeyProtected is not null)
            {
                existing.ApiKeyProtected = apiKeyProtected;
            }
            existing.ChatModel = chatModel;
            existing.Endpoint = endpoint;
        }
        else
        {
            _creds[(userId, key)] = new AiProviderCredential
            {
                UserId = userId,
                Provider = key,
                ApiKeyProtected = apiKeyProtected,
                ChatModel = chatModel,
                Endpoint = endpoint
            };
        }

        return Task.CompletedTask;
    }
}
