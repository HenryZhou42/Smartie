using Smartie.Domain.Entities;
using Smartie.Plugins.Abstractions;

namespace Smartie.Application.Abstractions;

public interface IPluginRepository
{
    Task<IReadOnlyList<PluginInstallation>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PluginInstallation?> FindAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<PluginInstallation?> FindForUpdateAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<PluginInstallation?> FindByKeyAsync(Guid userId, string pluginKey, CancellationToken cancellationToken = default);

    Task<PluginInstallation?> FindByKeyForUpdateAsync(Guid userId, string pluginKey, CancellationToken cancellationToken = default);

    Task<PluginInstallation> AddAsync(PluginInstallation installation, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PluginLogEntry>> ListLogsAsync(
        Guid userId,
        Guid pluginId,
        int take,
        CancellationToken cancellationToken = default);

    Task AddLogAsync(PluginLogEntry entry, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IPluginRegistry
{
    IReadOnlyList<RegisteredPluginCommand> GetCommands(bool enabledOnly = true);

    IReadOnlyList<RegisteredPluginPage> GetPages(string? pluginKey = null);

    IReadOnlyList<RegisteredPluginTool> GetTools(string? pluginKey = null);

    bool TryGetPage(string pluginKey, string pageId, out RegisteredPluginPage? page);

    IReadOnlyList<LoadedPluginSnapshot> GetLoadedSnapshots();

    IReadOnlyList<string> GetFailedPluginKeys();

    bool IsLoaded(string pluginKey);

    IReadOnlyList<RegisteredPluginCommand> GetCommandsForPlugin(string pluginKey);

    IReadOnlyList<RegisteredPluginPage> GetPagesForPlugin(string pluginKey);

    IReadOnlyList<RegisteredPluginTool> GetToolsForPlugin(string pluginKey);
}

public interface IPluginLoader
{
    Task<PluginLoadResult> LoadAsync(
        Guid userId,
        PluginInstallation installation,
        CancellationToken cancellationToken = default);

    Task UnloadAsync(string pluginKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiscoveredPluginFolder>> DiscoverAsync(CancellationToken cancellationToken = default);
}

public interface IPluginService
{
    Task<IReadOnlyList<PluginSnapshot>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PluginSnapshot?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<PluginScanSnapshot> ScanAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PluginSnapshot?> EnableAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<PluginSnapshot?> DisableAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<PluginSnapshot?> LoadAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<PluginSnapshot?> UnloadAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PluginLogSnapshot>> GetLogsAsync(
        Guid userId,
        Guid id,
        int take,
        CancellationToken cancellationToken = default);

    Task<PluginPageContentSnapshot?> GetPageContentAsync(
        Guid userId,
        string pluginKey,
        string pageId,
        CancellationToken cancellationToken = default);

    Task<PluginDeveloperSnapshot> GetDeveloperStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task LoadEnabledPluginsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record RegisteredPluginCommand(
    string PluginKey,
    string PluginName,
    string Id,
    string Title,
    string Description,
    string Icon,
    string Route,
    IReadOnlyList<string> Keywords,
    bool PluginEnabled);

public sealed record RegisteredPluginPage(
    string PluginKey,
    string Id,
    string Title,
    string Route,
    string? MarkupContent);

public sealed record RegisteredPluginTool(
    string PluginKey,
    string Id,
    string Name,
    string Description,
    string Category);

public sealed record LoadedPluginSnapshot(
    string PluginKey,
    string Name,
    string Version,
    long LoadDurationMs,
    int CommandCount,
    int PageCount,
    int ToolCount);

public sealed record PluginLoadResult(
    bool Success,
    long LoadDurationMs,
    string? Error,
    ISmartiePlugin? Instance);

public sealed record DiscoveredPluginFolder(
    string FolderName,
    string FolderPath,
    PluginManifest Manifest);

public sealed record PluginSnapshot(
    Guid Id,
    string PluginKey,
    string FolderName,
    string Name,
    string Description,
    string Version,
    string Author,
    string Category,
    string EntryAssembly,
    string? IconRelativePath,
    bool Enabled,
    bool IsLoaded,
    string? LoadError,
    long? LastLoadDurationMs,
    DateTimeOffset InstalledAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<RegisteredPluginCommand> Commands,
    IReadOnlyList<RegisteredPluginPage> Pages,
    IReadOnlyList<RegisteredPluginTool> Tools);

public sealed record PluginScanSnapshot(int DiscoveredCount, int NewCount, IReadOnlyList<PluginSnapshot> Plugins);

public sealed record PluginLogSnapshot(Guid Id, string Level, string Message, DateTimeOffset CreatedAt);

public sealed record PluginPageContentSnapshot(string PluginKey, string PageId, string Title, string? MarkupContent);

public sealed record PluginDeveloperSnapshot(
    int PluginCount,
    int LoadedCount,
    int FailedCount,
    long TotalLoadTimeMs,
    IReadOnlyList<string> LoadedPlugins,
    IReadOnlyList<string> FailedPlugins);
