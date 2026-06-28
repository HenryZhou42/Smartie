using Markdig;

namespace Smartie.Shared.Rendering;

/// <summary>Renders assistant Markdown to HTML for chat bubbles.</summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static string ToHtml(string? markdown) =>
        string.IsNullOrEmpty(markdown)
            ? string.Empty
            : Markdown.ToHtml(markdown, Pipeline);
}
