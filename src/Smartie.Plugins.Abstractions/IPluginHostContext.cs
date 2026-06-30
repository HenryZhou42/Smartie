namespace Smartie.Plugins.Abstractions;

public interface IPluginHostContext
{
    string PluginId { get; }

    string PluginDirectory { get; }

    IPluginLogger Logger { get; }

    IServiceProvider Services { get; }
}

public interface IPluginLogger
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}
