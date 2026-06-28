namespace Smartie.Shared.Services;

/// <summary>Web fallback — Knowledge page uses a hidden <c>InputFile</c> instead.</summary>
public sealed class WebDocumentFilePickerService : IDocumentFilePickerService
{
    public bool IsNativePickerAvailable => false;

    public Task<IReadOnlyList<PickedDocumentFile>> PickFilesAsync(
        IReadOnlyList<string> extensions,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PickedDocumentFile>>(Array.Empty<PickedDocumentFile>());
}
