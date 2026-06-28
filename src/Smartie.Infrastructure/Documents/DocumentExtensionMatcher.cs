namespace Smartie.Infrastructure.Documents;

internal static class DocumentExtensionMatcher
{
    public static bool IsAny(string storedExtension, params string[] expectedExtensions)
    {
        var normalized = Normalize(storedExtension);
        foreach (var expected in expectedExtensions)
        {
            if (string.Equals(normalized, Normalize(expected), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string extension) =>
        extension.Trim().TrimStart('.').ToLowerInvariant();
}
