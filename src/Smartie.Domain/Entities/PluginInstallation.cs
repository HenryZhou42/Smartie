namespace Smartie.Domain.Entities;

/// <summary>
/// Metadata and persisted state for a locally installed plugin.
/// </summary>
public class PluginInstallation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string PluginKey { get; set; } = string.Empty;

    public string FolderName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string Category { get; set; } = "Custom";

    public string EntryAssembly { get; set; } = string.Empty;

    public string? IconRelativePath { get; set; }

    public bool Enabled { get; set; } = true;

    public bool IsLoaded { get; set; }

    public string? LoadError { get; set; }

    public long? LastLoadDurationMs { get; set; }

    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<PluginLogEntry> Logs { get; set; } = new List<PluginLogEntry>();
}
