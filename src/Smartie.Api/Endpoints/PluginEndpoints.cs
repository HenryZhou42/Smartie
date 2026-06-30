using Smartie.Application.Abstractions;
using Smartie.Contracts;

namespace Smartie.Api.Endpoints;

public static class PluginEndpoints
{
    public static IEndpointRouteBuilder MapPluginEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/plugins");

        group.MapGet("/", async (IPluginService plugins, ICurrentUser user, CancellationToken ct) =>
        {
            var list = await plugins.ListAsync(user.UserId, ct);
            return Results.Ok(list.Select(ToDto));
        });

        group.MapPost("/scan", async (IPluginService plugins, ICurrentUser user, CancellationToken ct) =>
        {
            var scan = await plugins.ScanAsync(user.UserId, ct);
            return Results.Ok(new PluginScanResultDto(
                scan.DiscoveredCount,
                scan.NewCount,
                scan.Plugins.Select(ToDto).ToList()));
        });

        group.MapGet("/{id:guid}", async (Guid id, IPluginService plugins, ICurrentUser user, CancellationToken ct) =>
        {
            var plugin = await plugins.GetAsync(user.UserId, id, ct);
            return plugin is null ? Results.NotFound() : Results.Ok(ToDto(plugin));
        });

        group.MapPut("/{id:guid}/enable", async (Guid id, IPluginService plugins, ICurrentUser user, CancellationToken ct) =>
        {
            var plugin = await plugins.EnableAsync(user.UserId, id, ct);
            return plugin is null ? Results.NotFound() : Results.Ok(ToDto(plugin));
        });

        group.MapPut("/{id:guid}/disable", async (Guid id, IPluginService plugins, ICurrentUser user, CancellationToken ct) =>
        {
            var plugin = await plugins.DisableAsync(user.UserId, id, ct);
            return plugin is null ? Results.NotFound() : Results.Ok(ToDto(plugin));
        });

        group.MapPost("/{id:guid}/load", async (Guid id, IPluginService plugins, ICurrentUser user, CancellationToken ct) =>
        {
            var plugin = await plugins.LoadAsync(user.UserId, id, ct);
            return plugin is null ? Results.NotFound() : Results.Ok(ToDto(plugin));
        });

        group.MapPost("/{id:guid}/unload", async (Guid id, IPluginService plugins, ICurrentUser user, CancellationToken ct) =>
        {
            var plugin = await plugins.UnloadAsync(user.UserId, id, ct);
            return plugin is null ? Results.NotFound() : Results.Ok(ToDto(plugin));
        });

        group.MapGet("/{id:guid}/logs", async (Guid id, IPluginService plugins, ICurrentUser user, CancellationToken ct) =>
        {
            var logs = await plugins.GetLogsAsync(user.UserId, id, 100, ct);
            return Results.Ok(logs.Select(l => new PluginLogDto(l.Id, l.Level, l.Message, l.CreatedAt)));
        });

        group.MapGet("/pages/{pluginKey}/{pageId}", async (
            string pluginKey,
            string pageId,
            IPluginService plugins,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var page = await plugins.GetPageContentAsync(user.UserId, pluginKey, pageId, ct);
            return page is null
                ? Results.NotFound()
                : Results.Ok(new PluginPageContentDto(page.PluginKey, page.PageId, page.Title, page.MarkupContent));
        });

        group.MapGet("/developer", async (IPluginService plugins, ICurrentUser user, CancellationToken ct) =>
        {
            var stats = await plugins.GetDeveloperStatsAsync(user.UserId, ct);
            return Results.Ok(new PluginDeveloperDto(
                stats.PluginCount,
                stats.LoadedCount,
                stats.FailedCount,
                stats.TotalLoadTimeMs,
                stats.LoadedPlugins,
                stats.FailedPlugins));
        });

        return app;
    }

    private static PluginDto ToDto(PluginSnapshot plugin) =>
        new(
            plugin.Id,
            plugin.PluginKey,
            plugin.FolderName,
            plugin.Name,
            plugin.Description,
            plugin.Version,
            plugin.Author,
            plugin.Category,
            plugin.EntryAssembly,
            plugin.IconRelativePath,
            plugin.Enabled,
            plugin.IsLoaded,
            plugin.LoadError,
            plugin.LastLoadDurationMs,
            plugin.InstalledAt,
            plugin.UpdatedAt,
            plugin.Commands.Select(c => new PluginCommandDto(c.Id, c.Title, c.Description, c.Icon, c.Route, c.Keywords)).ToList(),
            plugin.Pages.Select(p => new PluginPageDto(p.Id, p.Title, p.Route, p.MarkupContent)).ToList(),
            plugin.Tools.Select(t => new PluginToolDto(t.Id, t.Name, t.Description, t.Category)).ToList());
}
