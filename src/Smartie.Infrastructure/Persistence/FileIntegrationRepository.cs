using Microsoft.EntityFrameworkCore;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Persistence;

public sealed class FileIntegrationRepository : IFileIntegrationRepository
{
    private readonly SmartieDbContext _db;

    public FileIntegrationRepository(SmartieDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RecentFile>> ListRecentAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _db.RecentFiles
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.Pinned)
            .ThenByDescending(f => f.LastOpenedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<RecentFile?> FindRecentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default) =>
        _db.RecentFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == userId && f.Id == id, cancellationToken);

    public Task<RecentFile?> FindRecentByPathAsync(
        Guid userId,
        string filePath,
        CancellationToken cancellationToken = default) =>
        _db.RecentFiles
            .FirstOrDefaultAsync(f => f.UserId == userId && f.FilePath == filePath, cancellationToken);

    public Task<RecentFile?> FindRecentForUpdateAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default) =>
        _db.RecentFiles.FirstOrDefaultAsync(f => f.UserId == userId && f.Id == id, cancellationToken);

    public async Task<RecentFile> AddRecentAsync(RecentFile file, CancellationToken cancellationToken = default)
    {
        _db.RecentFiles.Add(file);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return file;
    }

    public async Task<bool> DeleteRecentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var file = await _db.RecentFiles
            .FirstOrDefaultAsync(f => f.UserId == userId && f.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (file is null)
        {
            return false;
        }

        _db.RecentFiles.Remove(file);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task PruneRecentAsync(
        Guid userId,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        if (maxCount <= 0)
        {
            return;
        }

        var recent = await _db.RecentFiles
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.Pinned)
            .ThenByDescending(f => f.LastOpenedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (recent.Count <= maxCount)
        {
            return;
        }

        var pinnedCount = recent.Count(f => f.Pinned);
        var allowedUnpinned = Math.Max(0, maxCount - pinnedCount);
        var removable = recent
            .Where(f => !f.Pinned)
            .OrderByDescending(f => f.LastOpenedAt)
            .Skip(allowedUnpinned)
            .ToList();

        if (removable.Count == 0)
        {
            return;
        }

        _db.RecentFiles.RemoveRange(removable);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FavoriteFolder>> ListFavoriteFoldersAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _db.FavoriteFolders
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Label)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<FavoriteFolder?> FindFavoriteFolderAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default) =>
        _db.FavoriteFolders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == userId && f.Id == id, cancellationToken);

    public Task<FavoriteFolder?> FindFavoriteFolderByPathAsync(
        Guid userId,
        string folderPath,
        CancellationToken cancellationToken = default) =>
        _db.FavoriteFolders
            .FirstOrDefaultAsync(f => f.UserId == userId && f.FolderPath == folderPath, cancellationToken);

    public async Task<FavoriteFolder> AddFavoriteFolderAsync(
        FavoriteFolder folder,
        CancellationToken cancellationToken = default)
    {
        _db.FavoriteFolders.Add(folder);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return folder;
    }

    public async Task<bool> DeleteFavoriteFolderAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var folder = await _db.FavoriteFolders
            .FirstOrDefaultAsync(f => f.UserId == userId && f.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (folder is null)
        {
            return false;
        }

        _db.FavoriteFolders.Remove(folder);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<int> CountRecentAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.RecentFiles.AsNoTracking().CountAsync(f => f.UserId == userId, cancellationToken);

    public Task<int> CountFavoriteFoldersAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.FavoriteFolders.AsNoTracking().CountAsync(f => f.UserId == userId, cancellationToken);

    public Task<User?> GetUserSettingsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
