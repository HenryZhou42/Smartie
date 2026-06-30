namespace Smartie.Contracts;

/// <summary>Single source of truth for Smartie Community Edition product metadata (keep in sync with Directory.Build.props).</summary>
public static class ProductMetadata
{
    public const string ProductName = "Smartie";
    public const string ApplicationTitle = "Smartie Community Edition";
    public const string Edition = "Community Edition";
    public const string Description = "AI Productivity OS";
    public const string Version = "0.9.0";
    public const string ReleaseLabel = "RC";
    public const string Publisher = "Henry Zhou";
    public const string PackageIdentity = "Smartie.Community";
    public const string GitHubUrl = "https://github.com/smartie-ai/smartie";
    public const string License = "MIT";

    /// <summary>Build stamp; updated at release publish time.</summary>
    public const string BuildNumber = "2026.06.28";

    public static string FullVersion => $"{Version} ({ReleaseLabel})";
    public static string DisplayTitle => ApplicationTitle;
}
