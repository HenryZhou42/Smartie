namespace Smartie.Maui.Hosting;

internal static class WebView2Bootstrapper
{
    public static string? ResolveBrowserExecutableFolder()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && Directory.Exists(fromEnvironment))
        {
            return fromEnvironment;
        }

        foreach (var root in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                         "Microsoft", "EdgeWebView", "Application"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         "Microsoft", "EdgeWebView", "Application"),
                 })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var latest = Directory.GetDirectories(root)
                .Where(path => char.IsDigit(Path.GetFileName(path)[0]))
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (latest is not null)
            {
                return latest;
            }
        }

        return null;
    }

    public static string ResolveUserDataFolder()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Smartie",
            "WebView2");

        Directory.CreateDirectory(folder);
        return folder;
    }

    public static void ApplyProcessDefaults()
    {
        var browserFolder = ResolveBrowserExecutableFolder();
        if (!string.IsNullOrWhiteSpace(browserFolder))
        {
            Environment.SetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", browserFolder);
        }

        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", ResolveUserDataFolder());
    }
}
