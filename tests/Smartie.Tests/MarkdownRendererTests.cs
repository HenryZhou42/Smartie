using Smartie.Shared.Rendering;

namespace Smartie.Tests;

public sealed class MarkdownRendererTests
{
    [Fact]
    public void ToHtml_RendersHeadersBoldItalicAndCode()
    {
        const string markdown = """
            # Heading 1
            ## Heading 2
            ### Heading 3

            **bold text** and *italic text*

            Inline `SQLite` code.

            ```csharp
            var app = new Smartie();
            ```
            """;

        var html = MarkdownRenderer.ToHtml(markdown);

        Assert.Contains("<h1", html);
        Assert.Contains("Heading 1", html);
        Assert.Contains("<h2", html);
        Assert.Contains("<h3", html);
        Assert.Contains("<strong>bold text</strong>", html);
        Assert.Contains("<em>italic text</em>", html);
        Assert.Contains("<code>SQLite</code>", html);
        Assert.Contains("<pre>", html);
        Assert.Contains("language-csharp", html);
        Assert.Contains("var app = new Smartie();", html);
    }
}
