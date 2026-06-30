using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Automation;
using Smartie.Application.Configuration;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public sealed class FileIntegrationService : IFileIntegrationService
{
    private readonly IFileIntegrationRepository _repository;
    private readonly IDocumentRepository _documents;
    private readonly FileIntegrationOptions _options;
    private readonly ILogger<FileIntegrationService> _logger;
    private readonly IAutomationEventPublisher _automations;
    private long _lastSearchTimeMs;

    public FileIntegrationService(
        IFileIntegrationRepository repository,
        IDocumentRepository documents,
        IOptions<FileIntegrationOptions> options,
        ILogger<FileIntegrationService> logger,
        IAutomationEventPublisher automations)
    {
        _repository = repository;
        _documents = documents;
        _options = options.Value;
        _logger = logger;
        _automations = automations;
    }

    public async Task<IReadOnlyList<RecentFile>> ListRecentAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultFavoriteFoldersAsync(userId, cancellationToken).ConfigureAwait(false);
        return await _repository.ListRecentAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RecentFile> RecordRecentAsync(
        Guid userId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = Path.GetFullPath(filePath.Trim());
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("File was not found on this device.", normalizedPath);
        }

        var info = new FileInfo(normalizedPath);
        var extension = FileSearchHelper.NormalizeExtension(info.Extension);
        if (!FileSearchHelper.IsAllowedExtension(extension, _options.AllowedExtensions))
        {
            throw new InvalidOperationException($"File type '{extension}' is not supported.");
        }

        var settings = await GetSettingsInternalAsync(userId, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var existing = await _repository
            .FindRecentByPathAsync(userId, normalizedPath, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.FileName = info.Name;
            existing.Extension = extension;
            existing.Location = info.DirectoryName ?? string.Empty;
            existing.SizeBytes = info.Length;
            existing.LastOpenedAt = now;
            existing.UpdatedAt = now;
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _repository.PruneRecentAsync(userId, settings.MaxRecentFiles, cancellationToken).ConfigureAwait(false);
            return existing;
        }

        var created = new RecentFile
        {
            UserId = userId,
            FilePath = normalizedPath,
            FileName = info.Name,
            Extension = extension,
            Location = info.DirectoryName ?? string.Empty,
            SizeBytes = info.Length,
            LastOpenedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.AddRecentAsync(created, cancellationToken).ConfigureAwait(false);
        await _repository.PruneRecentAsync(userId, settings.MaxRecentFiles, cancellationToken).ConfigureAwait(false);

        await _automations.PublishAsync(
            userId,
            AutomationTriggerType.FileAdded,
            new AutomationEventContext(
                DocumentType: extension,
                Keyword: info.Name,
                EventDate: now),
            cancellationToken).ConfigureAwait(false);

        return created;
    }

    public async Task<RecentFile?> PinRecentAsync(
        Guid userId,
        Guid id,
        bool pinned,
        CancellationToken cancellationToken = default)
    {
        var file = await _repository.FindRecentForUpdateAsync(userId, id, cancellationToken).ConfigureAwait(false);
        if (file is null)
        {
            return null;
        }

        file.Pinned = pinned;
        file.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return file;
    }

    public async Task<RecentFile?> FavoriteRecentAsync(
        Guid userId,
        Guid id,
        bool favorite,
        CancellationToken cancellationToken = default)
    {
        var file = await _repository.FindRecentForUpdateAsync(userId, id, cancellationToken).ConfigureAwait(false);
        if (file is null)
        {
            return null;
        }

        file.IsFavorite = favorite;
        file.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return file;
    }

    public Task<bool> DeleteRecentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default) =>
        _repository.DeleteRecentAsync(userId, id, cancellationToken);

    public async Task<IReadOnlyList<FavoriteFolder>> ListFavoriteFoldersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultFavoriteFoldersAsync(userId, cancellationToken).ConfigureAwait(false);
        return await _repository.ListFavoriteFoldersAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<FavoriteFolder> AddFavoriteFolderAsync(
        Guid userId,
        string folderPath,
        string? label,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = Path.GetFullPath(folderPath.Trim());
        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException($"Folder was not found: {normalizedPath}");
        }

        var existing = await _repository
            .FindFavoriteFolderByPathAsync(userId, normalizedPath, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var folders = await _repository.ListFavoriteFoldersAsync(userId, cancellationToken).ConfigureAwait(false);
        var folder = new FavoriteFolder
        {
            UserId = userId,
            FolderPath = normalizedPath,
            Label = string.IsNullOrWhiteSpace(label) ? new DirectoryInfo(normalizedPath).Name : label.Trim(),
            SortOrder = folders.Count,
            AddedAt = DateTimeOffset.UtcNow
        };

        return await _repository.AddFavoriteFolderAsync(folder, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> RemoveFavoriteFolderAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default) =>
        _repository.DeleteFavoriteFolderAsync(userId, id, cancellationToken);

    public async Task<FileSearchResponse> SearchAsync(
        Guid userId,
        string query,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        await EnsureDefaultFavoriteFoldersAsync(userId, cancellationToken).ConfigureAwait(false);

        var settings = await GetSettingsInternalAsync(userId, cancellationToken).ConfigureAwait(false);
        var folders = await _repository.ListFavoriteFoldersAsync(userId, cancellationToken).ConfigureAwait(false);
        var matches = FileSearchHelper.SearchFavoriteFolders(
            folders.Select(f => f.FolderPath),
            query,
            settings.ShowHiddenFiles,
            _options.AllowedExtensions,
            _options.SearchMaxDepth,
            _options.SearchMaxResults);

        stopwatch.Stop();
        _lastSearchTimeMs = stopwatch.ElapsedMilliseconds;

        var results = matches
            .Select(m => new FileSearchResult(
                m.FilePath,
                m.FileName,
                m.Extension,
                m.Location,
                m.SizeBytes,
                m.ModifiedAt))
            .ToList();

        return new FileSearchResponse(
            results,
            new FileSearchDeveloperInfo(_lastSearchTimeMs, results.Count));
    }

    public async Task<FileIntegrationStats> GetStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultFavoriteFoldersAsync(userId, cancellationToken).ConfigureAwait(false);
        var recent = await _repository.ListRecentAsync(userId, cancellationToken).ConfigureAwait(false);
        var favorites = await _repository.ListFavoriteFoldersAsync(userId, cancellationToken).ConfigureAwait(false);
        var docs = await _documents.ListAsync(userId, search: null, cancellationToken).ConfigureAwait(false);
        var docStats = await _documents.GetStatsAsync(userId, cancellationToken).ConfigureAwait(false);

        var recentlyImported = docs
            .OrderByDescending(d => d.UploadedAt)
            .Take(5)
            .Select(d => new RecentImportInfo(d.Id, d.Name, d.FileName, d.UploadedAt))
            .ToList();

        return new FileIntegrationStats(
            recent.Count,
            favorites.Count,
            docStats.DocumentCount,
            recent.Take(8).ToList(),
            favorites.Take(8).ToList(),
            recentlyImported);
    }

    public async Task<FileIntegrationSettingsSnapshot> GetSettingsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsInternalAsync(userId, cancellationToken).ConfigureAwait(false);
        return new FileIntegrationSettingsSnapshot(settings.MaxRecentFiles, settings.ShowHiddenFiles);
    }

    public async Task UpdateSettingsAsync(
        Guid userId,
        FileIntegrationSettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserForUpdateAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"User {userId} was not found.");

        if (update.MaxRecentFiles is int maxRecent && maxRecent > 0)
        {
            user.FileMaxRecentFiles = maxRecent;
        }

        if (update.ShowHiddenFiles is bool showHidden)
        {
            user.FileShowHiddenFiles = showHidden;
        }

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (update.MaxRecentFiles is int limit && limit > 0)
        {
            await _repository.PruneRecentAsync(userId, limit, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<FileIntegrationDeveloperStats> GetDeveloperStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var recentCount = await _repository.CountRecentAsync(userId, cancellationToken).ConfigureAwait(false);
        var favoriteCount = await _repository.CountFavoriteFoldersAsync(userId, cancellationToken).ConfigureAwait(false);
        var docStats = await _documents.GetStatsAsync(userId, cancellationToken).ConfigureAwait(false);

        return new FileIntegrationDeveloperStats(
            recentCount,
            _lastSearchTimeMs,
            docStats.DocumentCount,
            favoriteCount);
    }

    public async Task EnsureDefaultFavoriteFoldersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.ListFavoriteFoldersAsync(userId, cancellationToken).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            return;
        }

        var defaults = BuildDefaultFavoriteFolders();
        var order = 0;
        foreach (var (path, label) in defaults)
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            try
            {
                await _repository.AddFavoriteFolderAsync(new FavoriteFolder
                {
                    UserId = userId,
                    FolderPath = Path.GetFullPath(path),
                    Label = label,
                    SortOrder = order++,
                    AddedAt = DateTimeOffset.UtcNow
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipped seeding favorite folder {FolderPath}.", path);
            }
        }
    }

    private static IEnumerable<(string Path, string Label)> BuildDefaultFavoriteFolders()
    {
        yield return (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents");
        yield return (Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Desktop");

        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        yield return (downloads, "Downloads");

        var projects = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Projects");
        yield return (projects, "Projects");

        var testData = FindTestDataFolder();
        if (testData is not null)
        {
            yield return (testData, "Smartie TestData");
        }
    }

    internal static string? FindTestDataFolder()
    {
        var directory = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(directory, "TestData");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var parent = Path.GetFullPath(Path.Combine(directory, ".."));
            if (parent == directory)
            {
                break;
            }

            directory = parent;
        }

        return null;
    }

    private async Task<(int MaxRecentFiles, bool ShowHiddenFiles)> GetSettingsInternalAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _repository.GetUserSettingsAsync(userId, cancellationToken).ConfigureAwait(false);
        return (
            user?.FileMaxRecentFiles ?? _options.DefaultMaxRecentFiles,
            user?.FileShowHiddenFiles ?? false);
    }
}
