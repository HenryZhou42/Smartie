namespace Smartie.Contracts;

public sealed record PluginDto(
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
    IReadOnlyList<PluginCommandDto> Commands,
    IReadOnlyList<PluginPageDto> Pages,
    IReadOnlyList<PluginToolDto> Tools);

public sealed record PluginCommandDto(
    string Id,
    string Title,
    string Description,
    string Icon,
    string Route,
    IReadOnlyList<string> Keywords);

public sealed record PluginPageDto(
    string Id,
    string Title,
    string Route,
    string? MarkupContent);

public sealed record PluginToolDto(
    string Id,
    string Name,
    string Description,
    string Category);

public sealed record PluginLogDto(
    Guid Id,
    string Level,
    string Message,
    DateTimeOffset CreatedAt);

public sealed record PluginPageContentDto(
    string PluginKey,
    string PageId,
    string Title,
    string? MarkupContent);

public sealed record PluginDeveloperDto(
    int PluginCount,
    int LoadedCount,
    int FailedCount,
    long TotalLoadTimeMs,
    IReadOnlyList<string> LoadedPlugins,
    IReadOnlyList<string> FailedPlugins);

public sealed record PluginScanResultDto(
    int DiscoveredCount,
    int NewCount,
    IReadOnlyList<PluginDto> Plugins);
