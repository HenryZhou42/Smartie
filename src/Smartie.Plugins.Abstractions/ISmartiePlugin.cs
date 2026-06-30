namespace Smartie.Plugins.Abstractions;

/// <summary>
/// Contract for local Smartie plugins loaded from trusted assemblies in the Plugins folder.
/// </summary>
public interface ISmartiePlugin
{
    string Name { get; }

    string Description { get; }

    string Version { get; }

    string Author { get; }

    string Icon { get; }

    string Category { get; }

    bool Enabled { get; set; }

    void Initialize(IPluginHostContext context);

    IReadOnlyList<PluginCommandDefinition> RegisterCommands();

    IReadOnlyList<PluginPageDefinition> RegisterPages();

    IReadOnlyList<PluginToolDefinition> RegisterTools();

    void Shutdown();
}
