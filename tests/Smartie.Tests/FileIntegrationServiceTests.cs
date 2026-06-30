using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Application.Services;
using Smartie.Domain.Entities;

namespace Smartie.Tests;

public class FileIntegrationServiceTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000401");
    private readonly string _root;
    private readonly InMemoryFileIntegrationRepository _repository = new(UserId);
    private readonly StubDocumentRepository _documents = new();
    private readonly FileIntegrationService _service;

    public FileIntegrationServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "smartie-file-service-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        _service = new FileIntegrationService(
            _repository,
            _documents,
            Options.Create(new FileIntegrationOptions()),
            NullLogger<FileIntegrationService>.Instance,
            NoOpAutomationEventPublisher.Instance);
    }

    [Fact]
    public async Task RecordRecentAsync_PersistsSupportedFile()
    {
        var filePath = Path.Combine(_root, "sample.txt");
        await File.WriteAllTextAsync(filePath, "hello");

        var recent = await _service.RecordRecentAsync(UserId, filePath);

        Assert.Equal("sample.txt", recent.FileName);
        Assert.Equal(".txt", recent.Extension);
        Assert.Single(await _service.ListRecentAsync(UserId));
    }

    [Fact]
    public async Task PinRecentAsync_UpdatesPinnedState()
    {
        var filePath = Path.Combine(_root, "pinned.txt");
        await File.WriteAllTextAsync(filePath, "hello");
        var recent = await _service.RecordRecentAsync(UserId, filePath);

        var pinned = await _service.PinRecentAsync(UserId, recent.Id, pinned: true);

        Assert.NotNull(pinned);
        Assert.True(pinned!.Pinned);
    }

    [Fact]
    public async Task AddFavoriteFolderAsync_PersistsFolder()
    {
        var folder = await _service.AddFavoriteFolderAsync(UserId, _root, "Test Root");

        Assert.Equal("Test Root", folder.Label);
        Assert.Contains(await _service.ListFavoriteFoldersAsync(UserId), f => f.Id == folder.Id);
    }

    [Fact]
    public async Task SearchAsync_FindsFilesInFavoriteFolders()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "Smartie_Search.txt"), "content");
        await _service.AddFavoriteFolderAsync(UserId, _root, "Search Root");

        var response = await _service.SearchAsync(UserId, "smartie");

        Assert.Single(response.Results);
        Assert.Equal("Smartie_Search.txt", response.Results[0].FileName);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ChangesMaxRecentFiles()
    {
        await _service.UpdateSettingsAsync(
            UserId,
            new FileIntegrationSettingsUpdate(MaxRecentFiles: 10, ShowHiddenFiles: true));

        var settings = await _service.GetSettingsAsync(UserId);

        Assert.Equal(10, settings.MaxRecentFiles);
        Assert.True(settings.ShowHiddenFiles);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for temp test folder.
        }
    }
}

internal sealed class InMemoryFileIntegrationRepository : IFileIntegrationRepository
{
    private readonly Dictionary<Guid, RecentFile> _recent = new();
    private readonly Dictionary<Guid, FavoriteFolder> _favorites = new();
    private User _user;

    public InMemoryFileIntegrationRepository(Guid userId)
    {
        _user = new User
        {
            Id = userId,
            DisplayName = "Test User",
            FileMaxRecentFiles = 50,
            FileShowHiddenFiles = false
        };
    }

    public Task<IReadOnlyList<RecentFile>> ListRecentAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RecentFile>>(_recent.Values
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.Pinned)
            .ThenByDescending(f => f.LastOpenedAt)
            .ToList());

    public Task<RecentFile?> FindRecentAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_recent.TryGetValue(id, out var file) && file.UserId == userId ? file : null);

    public Task<RecentFile?> FindRecentByPathAsync(Guid userId, string filePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(_recent.Values.FirstOrDefault(f => f.UserId == userId && f.FilePath == filePath));

    public Task<RecentFile?> FindRecentForUpdateAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        FindRecentAsync(userId, id, cancellationToken);

    public Task<RecentFile> AddRecentAsync(RecentFile file, CancellationToken cancellationToken = default)
    {
        _recent[file.Id] = file;
        return Task.FromResult(file);
    }

    public Task<bool> DeleteRecentAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        if (!_recent.TryGetValue(id, out var file) || file.UserId != userId)
        {
            return Task.FromResult(false);
        }

        _recent.Remove(id);
        return Task.FromResult(true);
    }

    public Task PruneRecentAsync(Guid userId, int maxCount, CancellationToken cancellationToken = default)
    {
        var recent = _recent.Values
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.Pinned)
            .ThenByDescending(f => f.LastOpenedAt)
            .ToList();

        if (recent.Count <= maxCount)
        {
            return Task.CompletedTask;
        }

        var pinnedCount = recent.Count(f => f.Pinned);
        var allowedUnpinned = Math.Max(0, maxCount - pinnedCount);
        foreach (var file in recent.Where(f => !f.Pinned).OrderByDescending(f => f.LastOpenedAt).Skip(allowedUnpinned))
        {
            _recent.Remove(file.Id);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FavoriteFolder>> ListFavoriteFoldersAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FavoriteFolder>>(_favorites.Values
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Label)
            .ToList());

    public Task<FavoriteFolder?> FindFavoriteFolderAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_favorites.TryGetValue(id, out var folder) && folder.UserId == userId ? folder : null);

    public Task<FavoriteFolder?> FindFavoriteFolderByPathAsync(Guid userId, string folderPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(_favorites.Values.FirstOrDefault(f => f.UserId == userId && f.FolderPath == folderPath));

    public Task<FavoriteFolder> AddFavoriteFolderAsync(FavoriteFolder folder, CancellationToken cancellationToken = default)
    {
        _favorites[folder.Id] = folder;
        return Task.FromResult(folder);
    }

    public Task<bool> DeleteFavoriteFolderAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        if (!_favorites.TryGetValue(id, out var folder) || folder.UserId != userId)
        {
            return Task.FromResult(false);
        }

        _favorites.Remove(id);
        return Task.FromResult(true);
    }

    public Task<int> CountRecentAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_recent.Values.Count(f => f.UserId == userId));

    public Task<int> CountFavoriteFoldersAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_favorites.Values.Count(f => f.UserId == userId));

    public Task<User?> GetUserSettingsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<User?>(_user.Id == userId ? _user : null);

    public Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<User?>(_user.Id == userId ? _user : null);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class StubDocumentRepository : IDocumentRepository
{
    public Task<IReadOnlyList<Document>> ListAsync(Guid userId, string? search, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());

    public Task<Document?> FindAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Document?>(null);

    public Task<DocumentStats> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DocumentStats(0, 0, 0, 0, 0, 0, null, null, 0, 0, 0, 0));

    public Task<Document> AddAsync(Document document, CancellationToken cancellationToken = default) =>
        Task.FromResult(document);

    public Task<Document?> UpdateNameAsync(Guid documentId, Guid userId, string name, CancellationToken cancellationToken = default) =>
        Task.FromResult<Document?>(null);

    public Task<bool> DeleteAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<Document?> FindForUpdateAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Document?>(null);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
