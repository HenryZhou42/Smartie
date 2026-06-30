using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IFileIntegrationRepository
{
    Task<IReadOnlyList<RecentFile>> ListRecentAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<RecentFile?> FindRecentAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<RecentFile?> FindRecentByPathAsync(Guid userId, string filePath, CancellationToken cancellationToken = default);

    Task<RecentFile?> FindRecentForUpdateAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<RecentFile> AddRecentAsync(RecentFile file, CancellationToken cancellationToken = default);

    Task<bool> DeleteRecentAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task PruneRecentAsync(Guid userId, int maxCount, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoriteFolder>> ListFavoriteFoldersAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<FavoriteFolder?> FindFavoriteFolderAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<FavoriteFolder?> FindFavoriteFolderByPathAsync(Guid userId, string folderPath, CancellationToken cancellationToken = default);

    Task<FavoriteFolder> AddFavoriteFolderAsync(FavoriteFolder folder, CancellationToken cancellationToken = default);

    Task<bool> DeleteFavoriteFolderAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<int> CountRecentAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> CountFavoriteFoldersAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<User?> GetUserSettingsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
