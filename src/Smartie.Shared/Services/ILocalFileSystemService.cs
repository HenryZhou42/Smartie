namespace Smartie.Shared.Services;

/// <summary>
/// Read-only desktop file system operations for Community Edition.
/// </summary>
public interface ILocalFileSystemService
{
    bool IsDesktopIntegrationAvailable { get; }

    Task OpenFileAsync(string filePath, CancellationToken cancellationToken = default);

    Task RevealInExplorerAsync(string filePath, CancellationToken cancellationToken = default);

    Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default);

    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);

    Task<string?> PickFileAsync(IReadOnlyList<string> extensions, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string filePath, CancellationToken cancellationToken = default);
}

public static class DesktopFileTypes
{
    public static readonly string[] SupportedExtensions =
        [".pdf", ".docx", ".txt", ".md", ".markdown", ".png", ".jpg", ".jpeg", ".csv"];
}
