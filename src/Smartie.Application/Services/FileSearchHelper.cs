using Smartie.Application.Configuration;

namespace Smartie.Application.Services;

public static class FileSearchHelper
{
    public static IReadOnlyList<FileSearchMatch> SearchFavoriteFolders(
        IEnumerable<string> folderPaths,
        string query,
        bool showHidden,
        IReadOnlyList<string> allowedExtensions,
        int maxDepth,
        int maxResults)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<FileSearchMatch>();
        }

        var normalizedQuery = query.Trim();
        var extensionSet = allowedExtensions
            .Select(NormalizeExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = new List<FileSearchMatch>();
        foreach (var folderPath in folderPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            SearchDirectory(
                folderPath,
                normalizedQuery,
                showHidden,
                extensionSet,
                maxDepth,
                currentDepth: 0,
                results,
                maxResults);

            if (results.Count >= maxResults)
            {
                break;
            }
        }

        return results
            .OrderByDescending(r => r.ModifiedAt)
            .Take(maxResults)
            .ToList();
    }

    public static bool IsAllowedExtension(string extension, IReadOnlyList<string> allowedExtensions)
    {
        var normalized = NormalizeExtension(extension);
        return allowedExtensions.Any(e => NormalizeExtension(e).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeExtension(string extension) =>
        extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";

    private static void SearchDirectory(
        string directoryPath,
        string query,
        bool showHidden,
        HashSet<string> allowedExtensions,
        int maxDepth,
        int currentDepth,
        List<FileSearchMatch> results,
        int maxResults)
    {
        if (results.Count >= maxResults || currentDepth > maxDepth)
        {
            return;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directoryPath);
        }
        catch
        {
            return;
        }

        foreach (var filePath in files)
        {
            if (results.Count >= maxResults)
            {
                return;
            }

            try
            {
                var info = new FileInfo(filePath);
                if (!showHidden && info.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    continue;
                }

                var extension = NormalizeExtension(info.Extension);
                if (!allowedExtensions.Contains(extension))
                {
                    continue;
                }

                if (!info.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                results.Add(new FileSearchMatch(
                    info.FullName,
                    info.Name,
                    extension,
                    info.DirectoryName ?? string.Empty,
                    info.Length,
                    info.LastWriteTimeUtc));
            }
            catch
            {
                // Skip unreadable files.
            }
        }

        if (currentDepth >= maxDepth)
        {
            return;
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(directoryPath);
        }
        catch
        {
            return;
        }

        foreach (var child in directories)
        {
            if (results.Count >= maxResults)
            {
                return;
            }

            try
            {
                var dirInfo = new DirectoryInfo(child);
                if (!showHidden && dirInfo.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    continue;
                }
            }
            catch
            {
                continue;
            }

            SearchDirectory(child, query, showHidden, allowedExtensions, maxDepth, currentDepth + 1, results, maxResults);
        }
    }

    public sealed record FileSearchMatch(
        string FilePath,
        string FileName,
        string Extension,
        string Location,
        long SizeBytes,
        DateTimeOffset ModifiedAt);
}
