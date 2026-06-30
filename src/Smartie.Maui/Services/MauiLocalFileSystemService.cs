using System.Diagnostics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Storage;
using Smartie.Shared.Services;

namespace Smartie.Maui.Services;

public sealed class MauiLocalFileSystemService : ILocalFileSystemService
{
    public bool IsDesktopIntegrationAvailable =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();

    public Task OpenFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File was not found.", filePath);
        }

        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    public Task RevealInExplorerAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File was not found.", filePath);
        }

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
            return Task.CompletedTask;
        }

        var folder = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException("Folder was not found.");
        }

        return OpenFolderAsync(folder, cancellationToken);
    }

    public Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder was not found: {folderPath}");
        }

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = true
            });
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", folderPath);
        }
        else
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }

        return Task.CompletedTask;
    }

    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add("*");

            var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
            if (window?.Handler?.PlatformView is MauiWinUIWindow mauiWindow)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mauiWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var folder = await picker.PickSingleFolderAsync().AsTask().ConfigureAwait(false);
            return folder?.Path;
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<string?> PickFileAsync(IReadOnlyList<string> extensions, CancellationToken cancellationToken = default)
    {
        var normalized = extensions
            .Select(e => e.StartsWith('.') ? e : $".{e}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var pickOptions = new PickOptions
        {
            PickerTitle = "Select a file",
            FileTypes = new FilePickerFileType(new Dictionary<Microsoft.Maui.Devices.DevicePlatform, IEnumerable<string>>
            {
                { Microsoft.Maui.Devices.DevicePlatform.WinUI, normalized },
                { Microsoft.Maui.Devices.DevicePlatform.macOS, normalized },
                { Microsoft.Maui.Devices.DevicePlatform.iOS, normalized },
                { Microsoft.Maui.Devices.DevicePlatform.Android, normalized }
            })
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await FilePicker.Default.PickAsync(pickOptions).ConfigureAwait(false);
            return result?.FullPath;
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            return null;
        }
    }

    public Task<Stream> OpenReadAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(File.OpenRead(filePath));
}
