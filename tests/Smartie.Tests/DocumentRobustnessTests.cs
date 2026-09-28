using Microsoft.Extensions.Options;
using Smartie.Application.Configuration;
using Smartie.Domain.Entities;
using Smartie.Infrastructure.Chunking;
using Smartie.Infrastructure.Documents;

namespace Smartie.Tests;

public class DocumentRobustnessTests
{
    [Theory]
    [InlineData("pdf")]
    [InlineData("docx")]
    public async Task CorruptDocument_RejectsInvalidContent(string extension)
    {
        var root = Path.Combine(Path.GetTempPath(), "smartie-corrupt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "broken." + extension), "This is not a valid container.");
            var storage = new TestDocumentStorage(root);
            var router = new DocumentTextExtractionRouter(
                new TxtDocumentTextExtractor(storage), new MarkdownDocumentTextExtractor(storage),
                new PdfDocumentTextExtractor(storage), new DocxDocumentTextExtractor(storage));
            await Assert.ThrowsAnyAsync<Exception>(() => router.ExtractTextAsync(
                new Document { Extension = extension, RelativePath = "broken." + extension }));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task LargeText_ProducesBoundedChunksAndKeepsTail()
    {
        var chunker = new BasicDocumentChunker(Options.Create(new ChunkingOptions()));
        var text = string.Concat(Enumerable.Repeat("Release validation paragraph with representative text.\n\n", 20000)) + "END_OF_DOCUMENT";
        var chunks = await chunker.ChunkAsync(new Document { Extension = "txt" }, text);
        Assert.True(chunks.Count > 100);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Content.Length, 1, 2500));
        Assert.Contains("END_OF_DOCUMENT", chunks[^1].Content);
    }

    [Fact]
    public async Task Chunking_HonorsAlreadyCancelledRequest()
    {
        var chunker = new BasicDocumentChunker(Options.Create(new ChunkingOptions()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chunker.ChunkAsync(
            new Document { Extension = "txt" }, "test", new CancellationToken(true)));
    }
}
