using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IFileIntegrationService
{
    Task<IReadOnlyList<RecentFile>> ListRecentAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<RecentFile> RecordRecentAsync(Guid userId, string filePath, CancellationToken cancellationToken = default);

    Task<RecentFile?> PinRecentAsync(Guid userId, Guid id, bool pinned, CancellationToken cancellationToken = default);

    Task<RecentFile?> FavoriteRecentAsync(Guid userId, Guid id, bool favorite, CancellationToken cancellationToken = default);

    Task<bool> DeleteRecentAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoriteFolder>> ListFavoriteFoldersAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<FavoriteFolder> AddFavoriteFolderAsync(Guid userId, string folderPath, string? label, CancellationToken cancellationToken = default);

    Task<bool> RemoveFavoriteFolderAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<FileSearchResponse> SearchAsync(Guid userId, string query, CancellationToken cancellationToken = default);

    Task<FileIntegrationStats> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<FileIntegrationSettingsSnapshot> GetSettingsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UpdateSettingsAsync(Guid userId, FileIntegrationSettingsUpdate update, CancellationToken cancellationToken = default);

    Task<FileIntegrationDeveloperStats> GetDeveloperStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task EnsureDefaultFavoriteFoldersAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record FileSearchResult(
    string FilePath,
    string FileName,
    string Extension,
    string Location,
    long SizeBytes,
    DateTimeOffset ModifiedAt);

public sealed record FileSearchResponse(
    IReadOnlyList<FileSearchResult> Results,
    FileSearchDeveloperInfo Developer);

public sealed record FileSearchDeveloperInfo(long SearchTimeMs, int ResultCount);

public sealed record FileIntegrationStats(
    int RecentFileCount,
    int FavoriteFolderCount,
    int IndexedDocumentCount,
    IReadOnlyList<RecentFile> RecentFiles,
    IReadOnlyList<FavoriteFolder> FavoriteFolders,
    IReadOnlyList<RecentImportInfo> RecentlyImported);

public sealed record RecentImportInfo(Guid DocumentId, string Name, string FileName, DateTimeOffset CreatedAt);

public sealed record FileIntegrationSettingsSnapshot(int MaxRecentFiles, bool ShowHiddenFiles);

public sealed record FileIntegrationSettingsUpdate(int? MaxRecentFiles, bool? ShowHiddenFiles);

public sealed record FileIntegrationDeveloperStats(
    int FileCount,
    long LastSearchTimeMs,
    int IndexedFiles,
    int FavoriteFolderCount);
