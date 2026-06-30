namespace Smartie.Maui.Hosting;

/// <summary>
/// MAUI/VS sometimes runs Smartie.exe from an RID subfolder (win-x64) while static web assets
/// land in the parent output directory. Copy wwwroot beside the running exe when needed.
/// </summary>
internal static class WebAssetsBootstrapper
{
    public static void EnsureHostPagePresent()
    {
        if (File.Exists(ResolveHostPagePath()))
        {
            return;
        }

        var baseDir = AppContext.BaseDirectory;
        foreach (var sourceRoot in EnumerateCandidateWwwRootDirectories(baseDir))
        {
            if (!File.Exists(Path.Combine(sourceRoot, "index.html")))
            {
                continue;
            }

            var targetRoot = Path.Combine(baseDir, "wwwroot");
            CopyDirectory(sourceRoot, targetRoot);
            break;
        }
    }

    public static string ResolveHostPagePath() =>
        Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");

    private static IEnumerable<string> EnumerateCandidateWwwRootDirectories(string baseDir)
    {
        yield return Path.Combine(baseDir, "wwwroot");

        var current = new DirectoryInfo(baseDir);
        for (var depth = 0; depth < 4 && current.Parent is not null; depth++)
        {
            current = current.Parent;
            yield return Path.Combine(current.FullName, "wwwroot");
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(sourceDir, targetDir, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var destination = file.Replace(sourceDir, targetDir, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}
