using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public sealed class MemoryService : IMemoryService
{
    private readonly IMemoryRepository _repository;
    private readonly IAiSettingsService _aiSettings;
    private readonly IEmbeddingProviderFactory _embeddingFactory;
    private readonly IMemoryExtractor _extractor;
    private readonly MemoryOptions _options;
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(
        IMemoryRepository repository,
        IAiSettingsService aiSettings,
        IEmbeddingProviderFactory embeddingFactory,
        IMemoryExtractor extractor,
        IOptions<MemoryOptions> options,
        ILogger<MemoryService> logger)
    {
        _repository = repository;
        _aiSettings = aiSettings;
        _embeddingFactory = embeddingFactory;
        _extractor = extractor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Memory> StoreMemoryAsync(
        Guid userId,
        string content,
        MemoryCategory category,
        MemoryImportance importance,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetUserSettingsInternalAsync(userId, cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("Memory is disabled in Settings.");
        }

        var trimmed = content.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Memory content must not be empty.", nameof(content));
        }

        await _repository
            .PruneExpiredAsync(userId, settings.RetentionDays, cancellationToken)
            .ConfigureAwait(false);

        var existing = await FindSimilarMemoryAsync(userId, trimmed, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await UpdateMemoryAsync(userId, existing.Id, trimmed, category, importance, cancellationToken)
                .ConfigureAwait(false)
                ?? existing;
        }

        var count = await _repository.CountAsync(userId, cancellationToken).ConfigureAwait(false);
        if (count >= settings.MaxMemories)
        {
            throw new InvalidOperationException(
                $"Maximum memory limit of {settings.MaxMemories} reached. Delete or pin fewer memories to add more.");
        }

        var memory = new Memory
        {
            UserId = userId,
            Content = trimmed,
            Category = category,
            Importance = importance,
            Pinned = importance == MemoryImportance.Pinned,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await ApplyEmbeddingAsync(userId, memory, cancellationToken).ConfigureAwait(false);
        return await _repository.AddAsync(memory, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MemorySearchResult>> SearchMemoryAsync(
        Guid userId,
        string query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetUserSettingsInternalAsync(userId, cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled || string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<MemorySearchResult>();
        }

        var effectiveTopK = topK > 0 ? topK : _options.DefaultSearchTopK;
        var searchable = await _repository.GetSearchableForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        if (searchable.Count == 0)
        {
            return Array.Empty<MemorySearchResult>();
        }

        var settingsResolved = await _aiSettings.ResolveEmbeddingAsync(userId, cancellationToken).ConfigureAwait(false);
        var provider = _embeddingFactory.Create(settingsResolved);
        var queryVector = await provider.GenerateEmbeddingAsync(query.Trim(), cancellationToken).ConfigureAwait(false);

        var scored = new List<MemorySearchResult>(searchable.Count);
        foreach (var row in searchable)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var vector = EmbeddingVectorConverter.FromBytes(row.EmbeddingVector);
            var score = CosineSimilarity.CalculateCosineSimilarity(queryVector, vector);
            if (row.Pinned)
            {
                score = Math.Min(1f, score + _options.PinnedScoreBoost);
            }

            if (score < _options.MinSimilarityScore && !row.Pinned)
            {
                continue;
            }

            scored.Add(new MemorySearchResult(
                row.MemoryId,
                row.Content,
                row.Category,
                row.Importance,
                score,
                row.Pinned,
                row.CreatedAt,
                row.LastReferencedAt));
        }

        var results = scored
            .OrderByDescending(r => r.Pinned)
            .ThenByDescending(r => r.Score)
            .ThenByDescending(r => r.CreatedAt)
            .Take(effectiveTopK)
            .ToList();

        await MarkReferencedAsync(userId, results.Select(r => r.MemoryId).ToList(), cancellationToken)
            .ConfigureAwait(false);

        return results;
    }

    public Task<bool> DeleteMemoryAsync(
        Guid userId,
        Guid memoryId,
        CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(userId, memoryId, cancellationToken);

    public async Task<Memory?> PinMemoryAsync(
        Guid userId,
        Guid memoryId,
        bool pinned,
        CancellationToken cancellationToken = default)
    {
        var memory = await _repository.FindForUpdateAsync(userId, memoryId, cancellationToken).ConfigureAwait(false);
        if (memory is null)
        {
            return null;
        }

        memory.Pinned = pinned;
        memory.Importance = pinned ? MemoryImportance.Pinned : memory.Importance == MemoryImportance.Pinned
            ? MemoryImportance.High
            : memory.Importance;
        memory.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return memory;
    }

    public async Task<Memory?> UpdateMemoryAsync(
        Guid userId,
        Guid memoryId,
        string content,
        MemoryCategory category,
        MemoryImportance importance,
        CancellationToken cancellationToken = default)
    {
        var trimmed = content.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Memory content must not be empty.", nameof(content));
        }

        var memory = await _repository.FindForUpdateAsync(userId, memoryId, cancellationToken).ConfigureAwait(false);
        if (memory is null)
        {
            return null;
        }

        memory.Content = trimmed;
        memory.Category = category;
        memory.Importance = importance;
        memory.Pinned = memory.Pinned || importance == MemoryImportance.Pinned;
        memory.UpdatedAt = DateTimeOffset.UtcNow;
        await ApplyEmbeddingAsync(userId, memory, cancellationToken).ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return memory;
    }

    public Task<IReadOnlyList<Memory>> ListMemoriesAsync(
        Guid userId,
        MemoryCategory? category,
        bool? pinnedOnly,
        CancellationToken cancellationToken = default) =>
        _repository.ListAsync(userId, category, pinnedOnly, cancellationToken);

    public async Task ExtractAndStoreFromUserMessageAsync(
        Guid userId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetUserSettingsInternalAsync(userId, cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled)
        {
            return;
        }

        foreach (var candidate in _extractor.Extract(userMessage))
        {
            try
            {
                await StoreMemoryAsync(
                        userId,
                        candidate.Content,
                        candidate.Category,
                        candidate.Importance,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                _logger.LogDebug(ex, "Skipped storing extracted memory for user {UserId}.", userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store extracted memory for user {UserId}.", userId);
            }
        }
    }

    public async Task<MemorySettingsSnapshot> GetSettingsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetUserSettingsInternalAsync(userId, cancellationToken).ConfigureAwait(false);
        var count = await _repository.CountAsync(userId, cancellationToken).ConfigureAwait(false);
        return new MemorySettingsSnapshot(settings.Enabled, settings.MaxMemories, settings.RetentionDays, count);
    }

    public async Task UpdateSettingsAsync(
        Guid userId,
        MemorySettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserForUpdateAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"User {userId} was not found.");

        if (update.Enabled is bool enabled)
        {
            user.MemoryEnabled = enabled;
        }

        if (update.MaxMemories is int maxMemories && maxMemories > 0)
        {
            user.MaxMemories = maxMemories;
        }

        if (update.RetentionDays is int retentionDays && retentionDays > 0)
        {
            user.MemoryRetentionDays = retentionDays;
        }

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<MemoryDeveloperStats> GetDeveloperStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memories = await _repository.ListAsync(userId, category: null, pinnedOnly: null, cancellationToken)
            .ConfigureAwait(false);
        int? dimensions = null;
        var sample = memories.FirstOrDefault(m => m.EmbeddingVector is { Length: > 0 })?.EmbeddingVector;
        if (sample is not null)
        {
            dimensions = EmbeddingVectorConverter.FromBytes(sample).Length;
        }

        return new MemoryDeveloperStats(
            memories.Count,
            memories.Count(m => m.Pinned),
            dimensions,
            _options.DefaultSearchTopK,
            _options.MinSimilarityScorePercent);
    }

    private async Task ApplyEmbeddingAsync(
        Guid userId,
        Memory memory,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _aiSettings.ResolveEmbeddingAsync(userId, cancellationToken).ConfigureAwait(false);
            var provider = _embeddingFactory.Create(settings);
            var vector = await provider.GenerateEmbeddingAsync(memory.Content, cancellationToken).ConfigureAwait(false);
            memory.EmbeddingVector = EmbeddingVectorConverter.ToBytes(vector);
            memory.EmbeddingModel = provider.ModelName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not generate embedding for memory {MemoryId}.", memory.Id);
            memory.EmbeddingVector = null;
            memory.EmbeddingModel = null;
        }
    }

    private async Task<Memory?> FindSimilarMemoryAsync(
        Guid userId,
        string content,
        CancellationToken cancellationToken)
    {
        var matches = await SearchMemoryAsync(userId, content, topK: 1, cancellationToken).ConfigureAwait(false);
        var top = matches.FirstOrDefault();
        if (top is null || top.Score < 0.85f)
        {
            return null;
        }

        return await _repository.FindAsync(userId, top.MemoryId, cancellationToken).ConfigureAwait(false);
    }

    private async Task MarkReferencedAsync(
        Guid userId,
        IReadOnlyList<Guid> memoryIds,
        CancellationToken cancellationToken)
    {
        if (memoryIds.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var memoryId in memoryIds)
        {
            var memory = await _repository.FindForUpdateAsync(userId, memoryId, cancellationToken).ConfigureAwait(false);
            if (memory is null)
            {
                continue;
            }

            memory.LastReferencedAt = now;
        }

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(bool Enabled, int MaxMemories, int RetentionDays)> GetUserSettingsInternalAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _repository.GetUserSettingsAsync(userId, cancellationToken).ConfigureAwait(false);
        return (
            user?.MemoryEnabled ?? true,
            user?.MaxMemories ?? _options.DefaultMaxMemories,
            user?.MemoryRetentionDays ?? _options.DefaultRetentionDays);
    }
}
