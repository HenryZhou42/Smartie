namespace Smartie.Contracts;

public sealed record RecentFileDto(
    Guid Id,
    string FilePath,
    string FileName,
    string Extension,
    string Location,
    long SizeBytes,
    bool Pinned,
    bool IsFavorite,
    DateTimeOffset LastOpenedAt,
    DateTimeOffset UpdatedAt);

public sealed record FavoriteFolderDto(
    Guid Id,
    string FolderPath,
    string Label,
    int SortOrder,
    DateTimeOffset AddedAt);

public sealed record RecordRecentFileRequest(string FilePath);

public sealed record PinRecentFileRequest(bool Pinned);

public sealed record FavoriteRecentFileRequest(bool IsFavorite);

public sealed record AddFavoriteFolderRequest(string FolderPath, string? Label);

public sealed record FileSearchRequest(string Query);

public sealed record FileSearchResultDto(
    string FilePath,
    string FileName,
    string Extension,
    string Location,
    long SizeBytes,
    DateTimeOffset ModifiedAt);

public sealed record FileSearchResponseDto(
    IReadOnlyList<FileSearchResultDto> Results,
    FileSearchDeveloperDto Developer);

public sealed record FileSearchDeveloperDto(long SearchTimeMs, int ResultCount);

public sealed record FileIntegrationStatsDto(
    int RecentFileCount,
    int FavoriteFolderCount,
    int IndexedDocumentCount,
    IReadOnlyList<RecentFileDto> RecentFiles,
    IReadOnlyList<FavoriteFolderDto> FavoriteFolders,
    IReadOnlyList<RecentImportDto> RecentlyImported);

public sealed record RecentImportDto(
    Guid DocumentId,
    string Name,
    string FileName,
    DateTimeOffset CreatedAt);

public sealed record FileIntegrationSettingsDto(
    int MaxRecentFiles,
    bool ShowHiddenFiles);

public sealed record UpdateFileIntegrationSettingsRequest(
    int? MaxRecentFiles,
    bool? ShowHiddenFiles);

public sealed record FileIntegrationDeveloperDto(
    int FileCount,
    long SearchTimeMs,
    int IndexedFiles,
    int FavoriteFolderCount);

public sealed record SupportedFileTypesDto(IReadOnlyList<string> Extensions);
