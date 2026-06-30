using Smartie.Contracts;

namespace Smartie.Shared;

/// <summary>UI-facing product metadata (delegates to <see cref="ProductMetadata"/>).</summary>
public static class SmartieAppInfo
{
    public const string ProductName = ProductMetadata.ProductName;
    public const string Edition = ProductMetadata.Edition;
    public const string Version = ProductMetadata.Version;
    public const string BuildNumber = ProductMetadata.BuildNumber;
    public const string ReleaseLabel = ProductMetadata.ReleaseLabel;
    public const string GitHubUrl = ProductMetadata.GitHubUrl;
    public const string License = ProductMetadata.License;
    public const string Description = ProductMetadata.Description;
    public const string ApplicationTitle = ProductMetadata.ApplicationTitle;

    public static string FullVersion => ProductMetadata.FullVersion;
    public static string DisplayTitle => ProductMetadata.DisplayTitle;

    public static string BuildDate => ProductMetadata.BuildNumber;
}
