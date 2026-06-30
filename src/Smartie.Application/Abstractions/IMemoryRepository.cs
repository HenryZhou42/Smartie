using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IMemoryRepository
{
    Task<IReadOnlyList<Memory>> ListAsync(
        Guid userId,
        MemoryCategory? category,
        bool? pinnedOnly,
        CancellationToken cancellationToken = default);

    Task<Memory?> FindAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default);

    Task<Memory?> FindForUpdateAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Memory> AddAsync(Memory memory, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchableMemoryRow>> GetSearchableForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<User?> GetUserSettingsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken = default);

    Task PruneExpiredAsync(Guid userId, int retentionDays, CancellationToken cancellationToken = default);
}
