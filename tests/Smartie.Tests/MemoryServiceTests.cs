using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Application.Services;
using Smartie.Domain.Entities;

namespace Smartie.Tests;

public class MemoryServiceTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000102");

    [Fact]
    public async Task StoreMemoryAsync_PersistsMemoryWithEmbedding()
    {
        var repository = new InMemoryMemoryRepository(UserId);
        var service = CreateService(repository);

        var memory = await service.StoreMemoryAsync(
            UserId,
            "Prefers Gemini",
            MemoryCategory.Preferences,
            MemoryImportance.High);

        Assert.Equal("Prefers Gemini", memory.Content);
        Assert.NotNull(memory.EmbeddingVector);
        Assert.Equal(1, await repository.CountAsync(UserId));
    }

    [Fact]
    public async Task SearchMemoryAsync_ReturnsRelevantMemory()
    {
        var repository = new InMemoryMemoryRepository(UserId);
        var service = CreateService(repository);

        await service.StoreMemoryAsync(UserId, "Prefers Gemini", MemoryCategory.Preferences, MemoryImportance.High);
        await service.StoreMemoryAsync(UserId, "Works in Edmonton", MemoryCategory.Work, MemoryImportance.Medium);

        var results = await service.SearchMemoryAsync(UserId, "Gemini preference", topK: 3);

        Assert.NotEmpty(results);
        Assert.Contains("Gemini", results[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PinMemoryAsync_SetsPinnedFlag()
    {
        var repository = new InMemoryMemoryRepository(UserId);
        var service = CreateService(repository);
        var stored = await service.StoreMemoryAsync(UserId, "Uses .NET", MemoryCategory.Technical, MemoryImportance.Medium);

        var pinned = await service.PinMemoryAsync(UserId, stored.Id, pinned: true);

        Assert.NotNull(pinned);
        Assert.True(pinned!.Pinned);
        Assert.Equal(MemoryImportance.Pinned, pinned.Importance);
    }

    [Fact]
    public async Task ExtractAndStoreFromUserMessageAsync_StoresPreference()
    {
        var repository = new InMemoryMemoryRepository(UserId);
        var service = CreateService(repository);

        await service.ExtractAndStoreFromUserMessageAsync(UserId, "I prefer Gemini.");

        var memories = await service.ListMemoriesAsync(UserId, category: null, pinnedOnly: null);
        Assert.Single(memories);
        Assert.Contains("Gemini", memories[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(MemoryCategory.Preferences, memories[0].Category);
    }

    [Fact]
    public async Task UpdateSettingsAsync_DisablesMemory()
    {
        var repository = new InMemoryMemoryRepository(UserId);
        var service = CreateService(repository);

        await service.UpdateSettingsAsync(UserId, new MemorySettingsUpdate(false, null, null));
        var settings = await service.GetSettingsAsync(UserId);

        Assert.False(settings.Enabled);
    }

    [Fact]
    public async Task StoreMemoryAsync_WhenDisabled_Throws()
    {
        var repository = new InMemoryMemoryRepository(UserId, memoryEnabled: false);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StoreMemoryAsync(UserId, "Prefers Gemini", MemoryCategory.Preferences, MemoryImportance.High));
    }

    private static MemoryService CreateService(InMemoryMemoryRepository repository)
    {
        var provider = new KeywordMemoryEmbeddingProvider();
        return new MemoryService(
            repository,
            new FakeEmbeddingAiSettingsService(),
            new FakeEmbeddingProviderFactory(provider),
            new MemoryExtractor(),
            Options.Create(new MemoryOptions { MinSimilarityScorePercent = 20 }),
            NullLogger<MemoryService>.Instance);
    }
}

internal sealed class KeywordMemoryEmbeddingProvider : IEmbeddingProvider
{
    private static readonly (string[] Terms, int Dimension)[] Topics =
    [
        (["gemini", "prefers gemini", "preference"], 0),
        (["smartie", "building smartie", "project"], 1),
        (["c#", "csharp", ".net", "dotnet"], 2),
        (["edmonton", "works in"], 3),
        (["ai", "interested"], 4)
    ];

    public string ProviderName => AiProviderCatalog.Google;

    public string ModelName => "keyword-memory-embedder";

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
        Task.FromResult(Embed(text));

    internal static float[] Embed(string text)
    {
        var vector = new float[Topics.Length];
        var normalized = text.ToLowerInvariant();

        foreach (var (terms, dimension) in Topics)
        {
            if (terms.Any(term => normalized.Contains(term, StringComparison.Ordinal)))
            {
                vector[dimension] += 1f;
            }
        }

        var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude <= 0f)
        {
            return vector;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= magnitude;
        }

        return vector;
    }
}

internal sealed class InMemoryMemoryRepository : IMemoryRepository
{
    private readonly Dictionary<Guid, Memory> _memories = new();
    private User _user;

    public InMemoryMemoryRepository(Guid userId, bool memoryEnabled = true)
    {
        _user = new User
        {
            Id = userId,
            DisplayName = "Test User",
            MemoryEnabled = memoryEnabled,
            MaxMemories = 200,
            MemoryRetentionDays = 365
        };
    }

    public Task<IReadOnlyList<Memory>> ListAsync(
        Guid userId,
        MemoryCategory? category,
        bool? pinnedOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _memories.Values.Where(m => m.UserId == userId);
        if (category is not null)
        {
            query = query.Where(m => m.Category == category);
        }

        if (pinnedOnly is true)
        {
            query = query.Where(m => m.Pinned);
        }

        return Task.FromResult<IReadOnlyList<Memory>>(
            query.OrderByDescending(m => m.UpdatedAt).ToList());
    }

    public Task<Memory?> FindAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_memories.TryGetValue(memoryId, out var memory) && memory.UserId == userId ? memory : null);

    public Task<Memory?> FindForUpdateAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default) =>
        FindAsync(userId, memoryId, cancellationToken);

    public Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_memories.Values.Count(m => m.UserId == userId));

    public Task<Memory> AddAsync(Memory memory, CancellationToken cancellationToken = default)
    {
        _memories[memory.Id] = memory;
        return Task.FromResult(memory);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> DeleteAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default)
    {
        if (!_memories.TryGetValue(memoryId, out var memory) || memory.UserId != userId)
        {
            return Task.FromResult(false);
        }

        _memories.Remove(memoryId);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<SearchableMemoryRow>> GetSearchableForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rows = _memories.Values
            .Where(m => m.UserId == userId && m.EmbeddingVector is not null)
            .Select(m => new SearchableMemoryRow(
                m.Id,
                m.Content,
                m.Category,
                m.Importance,
                m.Pinned,
                m.EmbeddingVector!,
                m.CreatedAt,
                m.LastReferencedAt))
            .ToList();

        return Task.FromResult<IReadOnlyList<SearchableMemoryRow>>(rows);
    }

    public Task<User?> GetUserSettingsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<User?>(_user.Id == userId ? _user : null);

    public Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        GetUserSettingsAsync(userId, cancellationToken);

    public Task PruneExpiredAsync(Guid userId, int retentionDays, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        foreach (var memory in _memories.Values.Where(m => m.UserId == userId && m.CreatedAt < cutoff).ToList())
        {
            _memories.Remove(memory.Id);
        }

        return Task.CompletedTask;
    }
}
