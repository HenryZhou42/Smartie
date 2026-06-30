namespace Smartie.Shared.Services;

/// <summary>
/// Global hook for opening the command palette from anywhere in the UI.
/// </summary>
public sealed class CommandPaletteHost
{
    public event Action? OpenRequested;

    public void RequestOpen() => OpenRequested?.Invoke();
}
