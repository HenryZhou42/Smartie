namespace Smartie.Domain.Entities;

public class PluginLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PluginInstallationId { get; set; }

    public PluginInstallation? PluginInstallation { get; set; }

    public string Level { get; set; } = "Info";

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
