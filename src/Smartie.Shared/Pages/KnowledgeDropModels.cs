namespace Smartie.Shared.Pages;

/// <summary>Result reported by the Knowledge Base drag-and-drop JavaScript helper.</summary>
public sealed class KnowledgeDropUploadResult
{
    public int UploadedCount { get; set; }

    public string[] UnsupportedFiles { get; set; } = [];

    public string[] TooLargeFiles { get; set; } = [];

    public string? Error { get; set; }
}
