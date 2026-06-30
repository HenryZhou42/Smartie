using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;
using Smartie.Plugins.Abstractions;

namespace Smartie.Application.Services;

public sealed class PluginService : IPluginService
{
    private readonly IPluginRepository _repository;
    private readonly IPluginLoader _loader;
    private readonly IPluginRegistry _registry;

    public PluginService(IPluginRepository repository, IPluginLoader loader, IPluginRegistry registry)
    {
        _repository = repository;
        _loader = loader;
        _registry = registry;
    }

    public async Task<IReadOnlyList<PluginSnapshot>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await DiscoverNewInstallationsAsync(userId, cancellationToken).ConfigureAwait(false);
        var installations = await _repository.ListAsync(userId, cancellationToken).ConfigureAwait(false);
        return installations.Select(ToSnapshot).ToList();
    }

    public async Task<PluginSnapshot?> GetAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var installation = await _repository.FindAsync(userId, id, cancellationToken).ConfigureAwait(false);
        return installation is null ? null : ToSnapshot(installation);
    }

    public async Task<PluginScanSnapshot> ScanAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var discovered = await _loader.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        var newCount = await DiscoverNewInstallationsAsync(userId, cancellationToken).ConfigureAwait(false);
        var installations = await _repository.ListAsync(userId, cancellationToken).ConfigureAwait(false);
        return new PluginScanSnapshot(
            discovered.Count,
            newCount,
            installations.Select(ToSnapshot).ToList());
    }

    private async Task<int> DiscoverNewInstallationsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var discovered = await _loader.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        var existing = await _repository.ListAsync(userId, cancellationToken).ConfigureAwait(false);
        var existingKeys = existing.Select(p => p.PluginKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newCount = 0;

        foreach (var folder in discovered)
        {
            if (existingKeys.Contains(folder.Manifest.Id))
            {
                continue;
            }

            var iconPath = File.Exists(Path.Combine(folder.FolderPath, "icon.png")) ? "icon.png" : null;
            await _repository.AddAsync(new PluginInstallation
            {
                UserId = userId,
                PluginKey = folder.Manifest.Id,
                FolderName = folder.FolderName,
                Name = folder.Manifest.Name,
                Description = folder.Manifest.Description,
                Version = folder.Manifest.Version,
                Author = folder.Manifest.Author,
                Category = NormalizeCategory(folder.Manifest.Category),
                EntryAssembly = folder.Manifest.EntryAssembly,
                IconRelativePath = iconPath,
                Enabled = true
            }, cancellationToken).ConfigureAwait(false);
            newCount++;
        }

        return newCount;
    }

    public async Task<PluginSnapshot?> EnableAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var installation = await GetForUpdateAsync(userId, id, cancellationToken).ConfigureAwait(false);
        if (installation is null)
        {
            return null;
        }

        installation.Enabled = true;
        installation.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await LoadAsync(userId, id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PluginSnapshot?> DisableAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var installation = await GetForUpdateAsync(userId, id, cancellationToken).ConfigureAwait(false);
        if (installation is null)
        {
            return null;
        }

        installation.Enabled = false;
        installation.IsLoaded = false;
        installation.UpdatedAt = DateTimeOffset.UtcNow;
        await _loader.UnloadAsync(installation.PluginKey, cancellationToken).ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToSnapshot(installation);
    }

    public async Task<PluginSnapshot?> LoadAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var installation = await GetForUpdateAsync(userId, id, cancellationToken).ConfigureAwait(false);
        if (installation is null)
        {
            return null;
        }

        if (!installation.Enabled)
        {
            installation.LoadError = "Plugin is disabled.";
            installation.IsLoaded = false;
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ToSnapshot(installation);
        }

        var result = await _loader.LoadAsync(userId, installation, cancellationToken).ConfigureAwait(false);
        installation.IsLoaded = result.Success;
        installation.LoadError = result.Error;
        installation.LastLoadDurationMs = result.LoadDurationMs;
        installation.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _repository.AddLogAsync(new PluginLogEntry
        {
            PluginInstallationId = installation.Id,
            Level = result.Success ? "Info" : "Error",
            Message = result.Success
                ? $"Plugin loaded in {result.LoadDurationMs} ms."
                : result.Error ?? "Plugin load failed."
        }, cancellationToken).ConfigureAwait(false);

        return ToSnapshot(installation);
    }

    public async Task<PluginSnapshot?> UnloadAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var installation = await GetForUpdateAsync(userId, id, cancellationToken).ConfigureAwait(false);
        if (installation is null)
        {
            return null;
        }

        await _loader.UnloadAsync(installation.PluginKey, cancellationToken).ConfigureAwait(false);
        installation.IsLoaded = false;
        installation.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _repository.AddLogAsync(new PluginLogEntry
        {
            PluginInstallationId = installation.Id,
            Level = "Info",
            Message = "Plugin unloaded."
        }, cancellationToken).ConfigureAwait(false);
        return ToSnapshot(installation);
    }

    public async Task<IReadOnlyList<PluginLogSnapshot>> GetLogsAsync(
        Guid userId,
        Guid id,
        int take,
        CancellationToken cancellationToken = default)
    {
        var logs = await _repository.ListLogsAsync(userId, id, take, cancellationToken).ConfigureAwait(false);
        return logs.Select(l => new PluginLogSnapshot(l.Id, l.Level, l.Message, l.CreatedAt)).ToList();
    }

    public Task<PluginPageContentSnapshot?> GetPageContentAsync(
        Guid userId,
        string pluginKey,
        string pageId,
        CancellationToken cancellationToken = default)
    {
        _ = userId;
        _ = cancellationToken;
        return Task.FromResult(_registry.TryGetPage(pluginKey, pageId, out var page) && page is not null
            ? new PluginPageContentSnapshot(pluginKey, pageId, page.Title, page.MarkupContent)
            : null);
    }

    public async Task<PluginDeveloperSnapshot> GetDeveloperStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var count = await _repository.CountAsync(userId, cancellationToken).ConfigureAwait(false);
        var loaded = _registry.GetLoadedSnapshots();
        var failed = _registry.GetFailedPluginKeys();
        return new PluginDeveloperSnapshot(
            count,
            loaded.Count,
            failed.Count,
            loaded.Sum(l => l.LoadDurationMs),
            loaded.Select(l => l.PluginKey).ToList(),
            failed);
    }

    public async Task LoadEnabledPluginsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await DiscoverNewInstallationsAsync(userId, cancellationToken).ConfigureAwait(false);
        var installations = await _repository.ListAsync(userId, cancellationToken).ConfigureAwait(false);
        foreach (var installation in installations.Where(p => p.Enabled && !p.IsLoaded))
        {
            await LoadAsync(userId, installation.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<PluginInstallation?> GetForUpdateAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken) =>
        _repository.FindForUpdateAsync(userId, id, cancellationToken);

    private PluginSnapshot ToSnapshot(PluginInstallation installation)
    {
        return new PluginSnapshot(
            installation.Id,
            installation.PluginKey,
            installation.FolderName,
            installation.Name,
            installation.Description,
            installation.Version,
            installation.Author,
            installation.Category,
            installation.EntryAssembly,
            installation.IconRelativePath,
            installation.Enabled,
            _registry.IsLoaded(installation.PluginKey),
            installation.LoadError,
            installation.LastLoadDurationMs,
            installation.InstalledAt,
            installation.UpdatedAt,
            _registry.GetCommandsForPlugin(installation.PluginKey),
            _registry.GetPagesForPlugin(installation.PluginKey),
            _registry.GetToolsForPlugin(installation.PluginKey));
    }

    private static string NormalizeCategory(string category) =>
        PluginCategories.All.FirstOrDefault(c => c.Equals(category, StringComparison.OrdinalIgnoreCase)) ?? "Custom";
}
