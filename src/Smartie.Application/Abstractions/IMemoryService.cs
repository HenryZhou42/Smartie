using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IMemoryService
{
    Task<Memory> StoreMemoryAsync(
        Guid userId,
        string content,
        MemoryCategory category,
        MemoryImportance importance,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemorySearchResult>> SearchMemoryAsync(
        Guid userId,
        string query,
        int topK,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteMemoryAsync(
        Guid userId,
        Guid memoryId,
        CancellationToken cancellationToken = default);

    Task<Memory?> PinMemoryAsync(
        Guid userId,
        Guid memoryId,
        bool pinned,
        CancellationToken cancellationToken = default);

    Task<Memory?> UpdateMemoryAsync(
        Guid userId,
        Guid memoryId,
        string content,
        MemoryCategory category,
        MemoryImportance importance,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Memory>> ListMemoriesAsync(
        Guid userId,
        MemoryCategory? category,
        bool? pinnedOnly,
        CancellationToken cancellationToken = default);

    Task ExtractAndStoreFromUserMessageAsync(
        Guid userId,
        string userMessage,
        CancellationToken cancellationToken = default);

    Task<MemorySettingsSnapshot> GetSettingsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task UpdateSettingsAsync(
        Guid userId,
        MemorySettingsUpdate update,
        CancellationToken cancellationToken = default);

    Task<MemoryDeveloperStats> GetDeveloperStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
