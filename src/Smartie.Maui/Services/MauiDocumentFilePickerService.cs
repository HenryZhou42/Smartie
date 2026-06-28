using Microsoft.Maui.Storage;
using Smartie.Shared.Services;

namespace Smartie.Maui.Services;

/// <summary>
/// Uses the OS file picker so uploads avoid Blazor WebView <c>InputFile</c>, which can
/// throw <see cref="System.Runtime.InteropServices.SEHException"/> on Windows.
/// </summary>
public sealed class MauiDocumentFilePickerService : IDocumentFilePickerService
{
    public bool IsNativePickerAvailable => true;

    public async Task<IReadOnlyList<PickedDocumentFile>> PickFilesAsync(
        IReadOnlyList<string> extensions,
        CancellationToken cancellationToken = default)
    {
        var normalized = extensions
            .Select(e => e.StartsWith('.') ? e : $".{e}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var pickOptions = new PickOptions
        {
            PickerTitle = "Select documents",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, normalized },
                { DevicePlatform.macOS, normalized },
                { DevicePlatform.iOS, normalized },
                { DevicePlatform.Android, normalized }
            })
        };

        IReadOnlyList<FileResult> results;
        try
        {
            var multiple = await FilePicker.Default.PickMultipleAsync(pickOptions).ConfigureAwait(false);
            results = multiple?.ToList() ?? new List<FileResult>();
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            return Array.Empty<PickedDocumentFile>();
        }

        var picked = new List<PickedDocumentFile>();
        foreach (var result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stream = await result.OpenReadAsync().ConfigureAwait(false);
            picked.Add(new PickedDocumentFile(result.FileName, stream));
        }

        return picked;
    }
}
