namespace Smartie.Contracts;

/// <summary>Single source of truth for Smartie Community Edition product metadata (keep in sync with Directory.Build.props).</summary>
public static class ProductMetadata
{
    public const string ProductName = "Smartie";
    public const string ApplicationTitle = "Smartie Community Edition";
    public const string Edition = "Community Edition";
    public const string Description = "AI Productivity OS";
    public const string Version = "0.9.0";
    public const string ReleaseLabel = "Beta";
    public const string Publisher = "Henry Zhou";
    public const string PackageIdentity = "Smartie.Community";
    public const string GitHubUrl = "https://github.com/HenryZhou42/Smartie";
    public const string License = "MIT";

    /// <summary>Build stamp; updated at release publish time.</summary>
    public static string BuildNumber =>
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
            typeof(ProductMetadata).Assembly)?.InformationalVersion.Split('+').ElementAtOrDefault(1)?.Split('.').Take(3)
            is { } parts ? string.Join(".", parts) : "unknown";

    public static string FullVersion => $"{Version} ({ReleaseLabel})";
    public static string DisplayTitle => ApplicationTitle;
}
