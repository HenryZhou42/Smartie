namespace Smartie.Plugins.Abstractions;

public static class PluginCategories
{
    public static readonly string[] All =
    [
        "Chat",
        "Knowledge Base",
        "Productivity",
        "Tasks",
        "Utilities",
        "Developer",
        "File Tools",
        "Integrations",
        "Custom"
    ];
}

public sealed record PluginManifest(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    string Category,
    string EntryAssembly,
    string MinimumSmartieVersion,
    IReadOnlyList<string> Dependencies);

public sealed record PluginCommandDefinition(
    string Id,
    string Title,
    string Description,
    string Icon,
    string Route,
    IReadOnlyList<string> Keywords);

public sealed record PluginPageDefinition(
    string Id,
    string Title,
    string Route,
    string? MarkupContent);

public sealed record PluginToolDefinition(
    string Id,
    string Name,
    string Description,
    string Category);
