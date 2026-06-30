using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Application.Services;
using Smartie.Domain.Entities;
using Smartie.Infrastructure.Chunking;

namespace Smartie.Tests;

public class CosineSimilarityTests
{
    [Fact]
    public void CalculateCosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var vector = new[] { 1f, 2f, 3f };
        var score = CosineSimilarity.CalculateCosineSimilarity(vector, vector);
        Assert.Equal(1f, score, precision: 5);
    }

    [Fact]
    public void CalculateCosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        var score = CosineSimilarity.CalculateCosineSimilarity(
            [1f, 0f, 0f],
            [0f, 1f, 0f]);

        Assert.Equal(0f, score, precision: 5);
    }
}

public class SemanticSearchServiceTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000099");

    public SemanticSearchServiceTests() => TestDocumentFixtures.EnsureFixtures();

    [Theory]
    [InlineData("vacation", "Vacation Policy")]
    [InlineData("training budget", "Training Budget")]
    [InlineData("engineering manager", "Sarah Johnson")]
    [InlineData("SQL Server", "Technology Stack")]
    public async Task SearchAsync_ReturnsExpectedChunk(string query, string expectedPhrase)
    {
        var service = await CreateServiceWithFixtureDocumentAsync();
        var result = await service.SearchAsync(UserId, query, topK: 5);

        Assert.NotEmpty(result.Results);
        Assert.Contains(result.Results, r => r.Content.Contains(expectedPhrase, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_UnrelatedQuery_ReturnsNoResults()
    {
        var service = await CreateServiceWithFixtureDocumentAsync();
        var result = await service.SearchAsync(UserId, "banana", topK: 5);

        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task SearchAsync_OrdersByScoreDescending()
    {
        var service = await CreateServiceWithFixtureDocumentAsync();
        var result = await service.SearchAsync(UserId, "vacation policy", topK: 5);

        Assert.True(result.Results.Count >= 2);
        Assert.True(result.Results[0].Score >= result.Results[1].Score);
    }

    private static async Task<SemanticSearchService> CreateServiceWithFixtureDocumentAsync()
    {
        var extractedText = await File.ReadAllTextAsync(TestDocumentFixtures.GetTestDataPath("Smartie_Test_Document.md"));
        var documentId = Guid.NewGuid();
        var document = new Document
        {
            Id = documentId,
            UserId = UserId,
            Name = "Smartie Test Document",
            FileName = "Smartie_Test_Document.md",
            Extension = "md",
            RelativePath = $"{documentId:N}/Smartie_Test_Document.md",
            SizeBytes = extractedText.Length,
            ExtractedText = extractedText,
            ExtractedLength = extractedText.Length,
            ExtractionStatus = DocumentExtractionStatus.Completed,
            IsChunked = true
        };

        var chunker = new BasicDocumentChunker(Options.Create(new ChunkingOptions()));
        var generated = await chunker.ChunkAsync(document, extractedText);
        var provider = new TopicSemanticEmbeddingProvider();
        var repository = new InMemorySearchChunkRepository(UserId, document);

        foreach (var chunk in generated)
        {
            var vector = await provider.GenerateEmbeddingAsync(chunk.Content);
            chunk.EmbeddingStatus = ChunkEmbeddingStatus.Completed;
            chunk.EmbeddingVector = EmbeddingVectorConverter.ToBytes(vector);
            chunk.EmbeddingModel = provider.ModelName;
        }

        await repository.ReplaceForDocumentAsync(documentId, generated);
        document.ChunkCount = generated.Count;

        return new SemanticSearchService(
            repository,
            new FakeEmbeddingAiSettingsService(),
            new FakeEmbeddingProviderFactory(provider),
            Options.Create(new SemanticSearchOptions()),
            NullLogger<SemanticSearchService>.Instance);
    }
}

internal sealed class TopicSemanticEmbeddingProvider : IEmbeddingProvider
{
    private static readonly (string[] Terms, int Dimension)[] Topics =
    [
        (["vacation", "vacation policy", "vacation days"], 0),
        (["training budget", "training", "budget", "professional development"], 1),
        (["engineering manager", "sarah johnson", "sarah", "johnson"], 2),
        (["sql server", "technology stack", "database", "postgresql", "sqlite"], 3),
        (["remote work", "remote", "hybrid"], 4)
    ];

    public string ProviderName => AiProviderCatalog.Google;

    public string ModelName => "topic-test-embedder";

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
        Task.FromResult(Embed(text));

    internal static float[] Embed(string text)
    {
        var vector = new float[Topics.Length];
        var normalized = text.ToLowerInvariant();

        foreach (var (terms, dimension) in Topics)
        {
            if (terms.Any(term => normalized.Contains(term, StringComparison.Ordinal)))
            {
                vector[dimension] += 1f;
            }
        }

        foreach (var token in normalized.Split([' ', ',', '.', ':', ';', '(', ')', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var (terms, dimension) in Topics)
            {
                if (terms.Any(term => term.Contains(token, StringComparison.Ordinal) || token.Contains(term, StringComparison.Ordinal)))
                {
                    vector[dimension] += 0.35f;
                }
            }
        }

        return Normalize(vector);
    }

    private static float[] Normalize(float[] vector)
    {
        var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude <= 0f)
        {
            return vector;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= magnitude;
        }

        return vector;
    }
}

internal sealed class InMemorySearchChunkRepository : IDocumentChunkRepository
{
    private readonly Guid _userId;
    private readonly Document _document;
    private readonly Dictionary<Guid, List<DocumentChunk>> _byDocument = new();

    public InMemorySearchChunkRepository(Guid userId, Document document)
    {
        _userId = userId;
        _document = document;
    }

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
        if (userId != _userId)
        {
            return Task.FromResult(new ChunkEmbeddingStats(0, 0, null));
        }

        var chunks = _byDocument.Values.SelectMany(c => c).ToList();
        var completed = chunks.Count(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Completed);
        var failed = chunks.Count(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Failed);
        int? dimension = chunks.FirstOrDefault(c => c.EmbeddingVector is { Length: > 0 })?.EmbeddingVector is { } bytes
            ? EmbeddingVectorConverter.FromBytes(bytes).Length
            : null;

        return Task.FromResult(new ChunkEmbeddingStats(completed, failed, dimension));
    }

    public Task<IReadOnlyList<SearchableChunkRow>> GetSearchableChunksForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId != _userId)
        {
            return Task.FromResult<IReadOnlyList<SearchableChunkRow>>(Array.Empty<SearchableChunkRow>());
        }

        var rows = _byDocument.Values
            .SelectMany(list => list)
            .Where(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Completed && c.EmbeddingVector is not null)
            .Select(c => new SearchableChunkRow(
                c.Id,
                c.DocumentId,
                _document.Name,
                _document.FileName,
                c.ChunkIndex,
                c.Content,
                c.PageNumber,
                c.EmbeddingVector!))
            .OrderBy(c => c.ChunkIndex)
            .ToList();

        return Task.FromResult<IReadOnlyList<SearchableChunkRow>>(rows);
    }
}
