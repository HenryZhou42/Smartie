namespace Smartie.Application.Configuration;

public sealed class KnowledgeBaseOptions
{
    public const string SectionName = "KnowledgeBase";

    public long MaxFileSizeBytes { get; set; } = 52_428_800;

    public string[] AllowedExtensions { get; set; } = [".pdf", ".docx", ".txt", ".md", ".markdown", ".png", ".jpg", ".jpeg", ".csv"];
}
