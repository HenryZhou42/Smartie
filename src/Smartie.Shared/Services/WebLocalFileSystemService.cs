using System.Diagnostics;

namespace Smartie.Shared.Services;

public sealed class WebLocalFileSystemService : ILocalFileSystemService
{
    public bool IsDesktopIntegrationAvailable => false;

    public Task OpenFileAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("Open file is available in the Smartie desktop app."));

    public Task RevealInExplorerAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("Reveal in Explorer is available in the Smartie desktop app."));

    public Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("Open folder is available in the Smartie desktop app."));

    public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task<string?> PickFileAsync(IReadOnlyList<string> extensions, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task<Stream> OpenReadAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(File.OpenRead(filePath));
}
