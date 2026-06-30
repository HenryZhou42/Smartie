using Microsoft.EntityFrameworkCore;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Persistence;

public sealed class MemoryRepository : IMemoryRepository
{
    private readonly SmartieDbContext _db;

    public MemoryRepository(SmartieDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Memory>> ListAsync(
        Guid userId,
        MemoryCategory? category,
        bool? pinnedOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Memories.AsNoTracking().Where(m => m.UserId == userId);

        if (category is MemoryCategory selectedCategory)
        {
            query = query.Where(m => m.Category == selectedCategory);
        }

        if (pinnedOnly is true)
        {
            query = query.Where(m => m.Pinned);
        }

        return await query
            .OrderByDescending(m => m.Pinned)
            .ThenByDescending(m => m.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<Memory?> FindAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default) =>
        _db.Memories
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Id == memoryId, cancellationToken);

    public Task<Memory?> FindForUpdateAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default) =>
        _db.Memories.FirstOrDefaultAsync(m => m.UserId == userId && m.Id == memoryId, cancellationToken);

    public Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Memories.AsNoTracking().CountAsync(m => m.UserId == userId, cancellationToken);

    public async Task<Memory> AddAsync(Memory memory, CancellationToken cancellationToken = default)
    {
        _db.Memories.Add(memory);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return memory;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task<bool> DeleteAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default)
    {
        var memory = await _db.Memories
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Id == memoryId, cancellationToken)
            .ConfigureAwait(false);
        if (memory is null)
        {
            return false;
        }

        _db.Memories.Remove(memory);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<SearchableMemoryRow>> GetSearchableForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _db.Memories
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.EmbeddingVector != null)
            .Select(m => new SearchableMemoryRow(
                m.Id,
                m.Content,
                m.Category,
                m.Importance,
                m.Pinned,
                m.EmbeddingVector!,
                m.CreatedAt,
                m.LastReferencedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<User?> GetUserSettingsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public async Task PruneExpiredAsync(Guid userId, int retentionDays, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        var expired = await _db.Memories
            .Where(m => m.UserId == userId && !m.Pinned && m.UpdatedAt < cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (expired.Count > 0)
        {
            _db.Memories.RemoveRange(expired);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
