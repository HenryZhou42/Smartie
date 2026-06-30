using Smartie.Application.Abstractions;
using Smartie.Application.Services;
using Smartie.Domain.Entities;
using Smartie.Plugins.Abstractions;

namespace Smartie.Tests;

public class PluginServiceTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000601");

    [Fact]
    public async Task ScanAsync_RegistersDiscoveredPlugin()
    {
        var repository = new InMemoryPluginRepository(UserId);
        var registry = new Smartie.Infrastructure.Plugins.PluginRegistry();
        var loader = new StubPluginLoader(registry);
        var service = new PluginService(repository, loader, registry);

        var scan = await service.ScanAsync(UserId);

        Assert.Equal(1, scan.DiscoveredCount);
        Assert.Equal(1, scan.NewCount);
        Assert.Single(scan.Plugins);
        Assert.Equal("example-plugin", scan.Plugins[0].PluginKey);
    }

    [Fact]
    public async Task LoadAsync_RegistersCommandsWhenSuccessful()
    {
        var repository = new InMemoryPluginRepository(UserId);
        var registry = new Smartie.Infrastructure.Plugins.PluginRegistry();
        var loader = new StubPluginLoader(registry);
        var service = new PluginService(repository, loader, registry);
        var scan = await service.ScanAsync(UserId);
        var pluginId = scan.Plugins[0].Id;

        var loaded = await service.LoadAsync(UserId, pluginId);

        Assert.NotNull(loaded);
        Assert.True(loaded!.IsLoaded);
        Assert.Single(registry.GetCommands());
    }

    [Fact]
    public async Task DisableAsync_UnloadsPlugin()
    {
        var repository = new InMemoryPluginRepository(UserId);
        var registry = new Smartie.Infrastructure.Plugins.PluginRegistry();
        var loader = new StubPluginLoader(registry);
        var service = new PluginService(repository, loader, registry);
        var pluginId = (await service.ScanAsync(UserId)).Plugins[0].Id;
        await service.LoadAsync(UserId, pluginId);

        var disabled = await service.DisableAsync(UserId, pluginId);

        Assert.NotNull(disabled);
        Assert.False(disabled!.Enabled);
        Assert.Contains("example-plugin", loader.UnloadedKeys);
    }
}

internal sealed class StubPluginLoader : IPluginLoader
{
    private readonly Smartie.Infrastructure.Plugins.PluginRegistry _registry;
    private readonly List<string> _unloaded = new();

    public StubPluginLoader(Smartie.Infrastructure.Plugins.PluginRegistry registry) => _registry = registry;

    public IReadOnlyList<string> UnloadedKeys => _unloaded;

    public Task<IReadOnlyList<DiscoveredPluginFolder>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var manifest = new PluginManifest(
            "example-plugin",
            "Example Plugin",
            "1.0.0",
            "Smartie",
            "Test plugin",
            "Developer",
            "Smartie.Plugins.ExamplePlugin.dll",
            "1.0.0",
            Array.Empty<string>());

        return Task.FromResult<IReadOnlyList<DiscoveredPluginFolder>>(
            [new DiscoveredPluginFolder("ExamplePlugin", "/Plugins/ExamplePlugin", manifest)]);
    }

    public Task<PluginLoadResult> LoadAsync(Guid userId, PluginInstallation installation, CancellationToken cancellationToken = default)
    {
        var plugin = new StubSmartiePlugin();
        var commands = plugin.RegisterCommands()
            .Select(c => new RegisteredPluginCommand(
                installation.PluginKey,
                plugin.Name,
                c.Id,
                c.Title,
                c.Description,
                c.Icon,
                c.Route,
                c.Keywords,
                true))
            .ToList();

        _registry.RegisterLoadedPlugin(
            installation.PluginKey,
            plugin,
            commands,
            Array.Empty<RegisteredPluginPage>(),
            Array.Empty<RegisteredPluginTool>(),
            12);

        return Task.FromResult(new PluginLoadResult(true, 12, null, plugin));
    }

    public Task UnloadAsync(string pluginKey, CancellationToken cancellationToken = default)
    {
        _unloaded.Add(pluginKey);
        _registry.Unregister(pluginKey);
        return Task.CompletedTask;
    }
}

internal sealed class StubSmartiePlugin : ISmartiePlugin
{
    public string Name => "Example Plugin";
    public string Description => "Stub";
    public string Version => "1.0.0";
    public string Author => "Smartie";
    public string Icon => "icon.png";
    public string Category => "Developer";
    public bool Enabled { get; set; } = true;
    public void Initialize(IPluginHostContext context) { }
    public IReadOnlyList<PluginCommandDefinition> RegisterCommands() =>
        [new("summarize", "Summarize Selection", "Summarize text.", "spark", "/plugins", ["summarize"])];
    public IReadOnlyList<PluginPageDefinition> RegisterPages() => [];
    public IReadOnlyList<PluginToolDefinition> RegisterTools() => [];
    public void Shutdown() { }
}

internal sealed class InMemoryPluginRepository : IPluginRepository
{
    private readonly Dictionary<Guid, PluginInstallation> _plugins = new();
    private readonly Guid _userId;

    public InMemoryPluginRepository(Guid userId) => _userId = userId;

    public Task<IReadOnlyList<PluginInstallation>> ListAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PluginInstallation>>(_plugins.Values.Where(p => p.UserId == userId).ToList());

    public Task<PluginInstallation?> FindAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_plugins.TryGetValue(id, out var plugin) && plugin.UserId == userId ? plugin : null);

    public Task<PluginInstallation?> FindForUpdateAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        FindAsync(userId, id, cancellationToken);

    public Task<PluginInstallation?> FindByKeyAsync(Guid userId, string pluginKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_plugins.Values.FirstOrDefault(p => p.UserId == userId && p.PluginKey == pluginKey));

    public Task<PluginInstallation?> FindByKeyForUpdateAsync(Guid userId, string pluginKey, CancellationToken cancellationToken = default) =>
        FindByKeyAsync(userId, pluginKey, cancellationToken);

    public Task<PluginInstallation> AddAsync(PluginInstallation installation, CancellationToken cancellationToken = default)
    {
        _plugins[installation.Id] = installation;
        return Task.FromResult(installation);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<PluginLogEntry>> ListLogsAsync(Guid userId, Guid pluginId, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PluginLogEntry>>(Array.Empty<PluginLogEntry>());

    public Task AddLogAsync(PluginLogEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_plugins.Values.Count(p => p.UserId == userId));
}
