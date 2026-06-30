using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Application.Services;
using Smartie.Domain.Entities;
using Smartie.Infrastructure.Chunking;

namespace Smartie.Tests;

public class DocumentChunkingServiceTests
{
    public DocumentChunkingServiceTests() => TestDocumentFixtures.EnsureFixtures();

    [Fact]
    public async Task ChunkAndPersistAsync_CreatesChunksForMarkdownDocument()
    {
        var fixturePath = TestDocumentFixtures.GetTestDataPath("Smartie_Test_Document.md");
        var extractedText = await File.ReadAllTextAsync(fixturePath);
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var repository = new ExtractionTestDocumentRepository(new Document
        {
            Id = documentId,
            UserId = userId,
            Name = "Smartie Test Document",
            FileName = "Smartie_Test_Document.md",
            Extension = "md",
            RelativePath = $"{documentId:N}/Smartie_Test_Document.md",
            SizeBytes = extractedText.Length,
            ExtractedText = extractedText,
            ExtractedLength = extractedText.Length,
            ExtractionStatus = DocumentExtractionStatus.Completed
        });

        var chunkRepository = new InMemoryDocumentChunkRepository();
        var chunker = new BasicDocumentChunker(Options.Create(new ChunkingOptions()));
        var service = new DocumentChunkingService(
            repository,
            chunkRepository,
            chunker,
            new NoOpDocumentEmbeddingService(repository),
            NullLogger<DocumentChunkingService>.Instance);

        var result = await service.ChunkAndPersistAsync(documentId, userId);

        Assert.True(result.IsChunked);
        Assert.True(result.ChunkCount > 1);
        Assert.NotNull(result.ChunkedAt);

        var chunks = await chunkRepository.GetForDocumentAsync(documentId);
        Assert.Equal(result.ChunkCount, chunks.Count);
        Assert.Contains(chunks, c => c.Content.Contains("Vacation Policy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chunks, c => c.Content.Contains("Sarah Johnson", StringComparison.OrdinalIgnoreCase));
        Assert.All(chunks, c => Assert.InRange(c.CharacterCount, 1, 2600));
    }

    [Fact]
    public async Task RebuildChunksAsync_ReplacesExistingChunks()
    {
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var extractedText = "Paragraph one about vacation policy.\n\nParagraph two mentions Sarah Johnson.\n\nParagraph three covers benefits.";

        var repository = new ExtractionTestDocumentRepository(new Document
        {
            Id = documentId,
            UserId = userId,
            Name = "Policy",
            FileName = "policy.txt",
            Extension = "txt",
            RelativePath = $"{documentId:N}/policy.txt",
            SizeBytes = extractedText.Length,
            ExtractedText = extractedText,
            ExtractedLength = extractedText.Length,
            ExtractionStatus = DocumentExtractionStatus.Completed,
            IsChunked = true,
            ChunkCount = 1
        });

        var chunkRepository = new InMemoryDocumentChunkRepository();
        await chunkRepository.ReplaceForDocumentAsync(documentId,
        [
            new DocumentChunk
            {
                DocumentId = documentId,
                ChunkIndex = 0,
                Content = "old chunk",
                CharacterCount = 9,
                TokenEstimate = 3,
                StartPosition = 0,
                EndPosition = 9
            }
        ]);

        var service = new DocumentChunkingService(
            repository,
            chunkRepository,
            new BasicDocumentChunker(Options.Create(new ChunkingOptions { TargetChunkSize = 80, MaxChunkSize = 120, ChunkOverlap = 20 })),
            new NoOpDocumentEmbeddingService(repository),
            NullLogger<DocumentChunkingService>.Instance);

        var rebuilt = await service.RebuildChunksAsync(documentId, userId);
        var chunks = await chunkRepository.GetForDocumentAsync(documentId);

        Assert.True(rebuilt.ChunkCount > 1);
        Assert.DoesNotContain(chunks, c => c.Content == "old chunk");
        Assert.Contains(chunks, c => c.Content.Contains("Sarah Johnson", StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class InMemoryDocumentChunkRepository : Smartie.Application.Abstractions.IDocumentChunkRepository
{
    private readonly Dictionary<Guid, List<DocumentChunk>> _byDocument = new();

    public Task ReplaceForDocumentAsync(
        Guid documentId,
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        _byDocument[documentId] = chunks.Select(c =>
        {
            c.DocumentId = documentId;
            return c;
        }).ToList();
        return Task.CompletedTask;
    }

    public Task DeleteForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        _byDocument.Remove(documentId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentChunk>> GetForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DocumentChunk>>(
            _byDocument.TryGetValue(documentId, out var list)
                ? list.OrderBy(c => c.ChunkIndex).ToList()
                : Array.Empty<DocumentChunk>());

    public Task<int> GetCountForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byDocument.TryGetValue(documentId, out var list) ? list.Count : 0);

    public Task<double> GetAverageLengthForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        if (!_byDocument.TryGetValue(documentId, out var list) || list.Count == 0)
        {
            return Task.FromResult(0d);
        }

        return Task.FromResult(list.Average(c => (double)c.CharacterCount));
    }

    public Task<IReadOnlyList<DocumentChunk>> GetTrackedForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        GetForDocumentAsync(documentId, cancellationToken);

    public Task<ChunkEmbeddingStats> GetEmbeddingStatsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var allChunks = _byDocument.Values.SelectMany(c => c).ToList();
        var completed = allChunks.Count(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Completed);
        var failed = allChunks.Count(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Failed);
        int? dimension = null;
        var sample = allChunks.FirstOrDefault(c => c.EmbeddingVector is { Length: > 0 })?.EmbeddingVector;
        if (sample is not null)
        {
            dimension = EmbeddingVectorConverter.FromBytes(sample).Length;
        }

        return Task.FromResult(new ChunkEmbeddingStats(completed, failed, dimension));
    }

    public Task<IReadOnlyList<SearchableChunkRow>> GetSearchableChunksForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rows = _byDocument.Values
            .SelectMany(list => list)
            .Where(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Completed && c.EmbeddingVector is not null)
            .Select(c => new SearchableChunkRow(
                c.Id,
                c.DocumentId,
                "Document",
                "document.bin",
                c.ChunkIndex,
                c.Content,
                c.PageNumber,
                c.EmbeddingVector!))
            .ToList();

        return Task.FromResult<IReadOnlyList<SearchableChunkRow>>(rows);
    }
}
