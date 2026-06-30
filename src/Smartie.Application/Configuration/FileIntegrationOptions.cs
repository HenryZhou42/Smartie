namespace Smartie.Application.Configuration;

public sealed class FileIntegrationOptions
{
    public const string SectionName = "Files";

    public int DefaultMaxRecentFiles { get; set; } = 50;

    public string[] AllowedExtensions { get; set; } =
        [".pdf", ".docx", ".txt", ".md", ".markdown", ".png", ".jpg", ".jpeg", ".csv"];

    public int SearchMaxResults { get; set; } = 100;

    public int SearchMaxDepth { get; set; } = 4;
}
