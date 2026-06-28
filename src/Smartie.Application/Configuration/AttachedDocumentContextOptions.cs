namespace Smartie.Application.Configuration;

public sealed class AttachedDocumentContextOptions
{
    public const string SectionName = "AttachedDocuments";

    public int MaxTotalCharacters { get; set; } = 30_000;

    /// <summary>When true, logs a preview of the augmented prompt at Debug level.</summary>
    public bool LogPromptPreview { get; set; }
}
