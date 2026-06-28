namespace Smartie.Shared.Services;

/// <summary>
/// Picks local documents for Knowledge Base upload. MAUI uses the native file picker
/// because Blazor <c>InputFile</c> can crash WebView2 on Windows (SEHException).
/// </summary>
public interface IDocumentFilePickerService
{
    bool IsNativePickerAvailable { get; }

    Task<IReadOnlyList<PickedDocumentFile>> PickFilesAsync(
        IReadOnlyList<string> extensions,
        CancellationToken cancellationToken = default);
}

public sealed class PickedDocumentFile : IAsyncDisposable
{
    public PickedDocumentFile(string fileName, Stream content)
    {
        FileName = fileName;
        Content = content;
    }

    public string FileName { get; }

    public Stream Content { get; }

    public ValueTask DisposeAsync()
    {
        Content.Dispose();
        return ValueTask.CompletedTask;
    }
}
