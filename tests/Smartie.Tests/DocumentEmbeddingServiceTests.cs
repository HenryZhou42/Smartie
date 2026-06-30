using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Application.Services;
using Smartie.Domain.Entities;

namespace Smartie.Tests;

public class DocumentEmbeddingServiceTests
{
    [Fact]
    public async Task GenerateAndPersistAsync_StoresEmbeddingsForAllChunks()
    {
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var repository = new ExtractionTestDocumentRepository(new Document
        {
            Id = documentId,
            UserId = userId,
            Name = "Policy",
            FileName = "policy.txt",
            Extension = "txt",
            RelativePath = $"{documentId:N}/policy.txt",
            SizeBytes = 100,
            IsChunked = true,
            ChunkCount = 2
        });

        var chunkRepository = new InMemoryDocumentChunkRepository();
        await chunkRepository.ReplaceForDocumentAsync(documentId,
        [
            new DocumentChunk
            {
                DocumentId = documentId,
                ChunkIndex = 0,
                Content = "Vacation policy details.",
                CharacterCount = 24,
                TokenEstimate = 6,
                StartPosition = 0,
                EndPosition = 24
            },
            new DocumentChunk
            {
                DocumentId = documentId,
                ChunkIndex = 1,
                Content = "Benefits overview for employees.",
                CharacterCount = 32,
                TokenEstimate = 8,
                StartPosition = 25,
                EndPosition = 57
            }
        ]);

        var service = CreateService(repository, chunkRepository, new DeterministicEmbeddingProvider());

        var result = await service.GenerateAndPersistAsync(documentId, userId);

        Assert.True(result.IsEmbedded);
        Assert.Equal(2, result.EmbeddedChunkCount);
        Assert.Equal("fake-embedding-model", result.EmbeddingModel);
        Assert.NotNull(result.EmbeddedAt);

        var chunks = await chunkRepository.GetForDocumentAsync(documentId);
        Assert.All(chunks, c => Assert.Equal(ChunkEmbeddingStatus.Completed, c.EmbeddingStatus));
        Assert.All(chunks, c => Assert.NotNull(c.EmbeddingVector));
        Assert.All(chunks, c => Assert.Equal(4, EmbeddingVectorConverter.FromBytes(c.EmbeddingVector!).Length));
    }

    [Fact]
    public async Task RebuildEmbeddingsAsync_ReplacesExistingEmbeddings()
    {
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var repository = new ExtractionTestDocumentRepository(new Document
        {
            Id = documentId,
            UserId = userId,
            Name = "Policy",
            FileName = "policy.txt",
            Extension = "txt",
            RelativePath = $"{documentId:N}/policy.txt",
            SizeBytes = 100,
            IsChunked = true,
            ChunkCount = 1,
            IsEmbedded = true,
            EmbeddedChunkCount = 1,
            EmbeddingModel = "old-model"
        });

        var chunkRepository = new InMemoryDocumentChunkRepository();
        await chunkRepository.ReplaceForDocumentAsync(documentId,
        [
            new DocumentChunk
            {
                DocumentId = documentId,
                ChunkIndex = 0,
                Content = "Updated chunk content.",
                CharacterCount = 22,
                TokenEstimate = 6,
                StartPosition = 0,
                EndPosition = 22,
                EmbeddingStatus = ChunkEmbeddingStatus.Completed,
                EmbeddingModel = "old-model",
                EmbeddingVector = EmbeddingVectorConverter.ToBytes([0.1f, 0.2f, 0.3f, 0.4f])
            }
        ]);

        var provider = new DeterministicEmbeddingProvider();
        var service = CreateService(repository, chunkRepository, provider);

        var result = await service.RebuildEmbeddingsAsync(documentId, userId);
        var chunks = await chunkRepository.GetForDocumentAsync(documentId);

        Assert.True(result.IsEmbedded);
        Assert.Equal("fake-embedding-model", result.EmbeddingModel);
        Assert.Equal(ChunkEmbeddingStatus.Completed, chunks[0].EmbeddingStatus);
        Assert.NotEqual([0.1f, 0.2f, 0.3f, 0.4f], EmbeddingVectorConverter.FromBytes(chunks[0].EmbeddingVector!));
    }

    [Fact]
    public async Task GenerateAndPersistAsync_ContinuesWhenSingleChunkFails()
    {
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var repository = new ExtractionTestDocumentRepository(new Document
        {
            Id = documentId,
            UserId = userId,
            Name = "Policy",
            FileName = "policy.txt",
            Extension = "txt",
            RelativePath = $"{documentId:N}/policy.txt",
            SizeBytes = 100,
            IsChunked = true,
            ChunkCount = 2
        });

        var chunkRepository = new InMemoryDocumentChunkRepository();
        await chunkRepository.ReplaceForDocumentAsync(documentId,
        [
            new DocumentChunk
            {
                DocumentId = documentId,
                ChunkIndex = 0,
                Content = "fail-me",
                CharacterCount = 7,
                TokenEstimate = 2,
                StartPosition = 0,
                EndPosition = 7
            },
            new DocumentChunk
            {
                DocumentId = documentId,
                ChunkIndex = 1,
                Content = "Benefits overview.",
                CharacterCount = 18,
                TokenEstimate = 5,
                StartPosition = 8,
                EndPosition = 26
            }
        ]);

        var service = CreateService(repository, chunkRepository, new DeterministicEmbeddingProvider());

        var result = await service.GenerateAndPersistAsync(documentId, userId);
        var chunks = await chunkRepository.GetForDocumentAsync(documentId);

        Assert.False(result.IsEmbedded);
        Assert.Equal(1, result.EmbeddedChunkCount);
        Assert.Equal(ChunkEmbeddingStatus.Failed, chunks[0].EmbeddingStatus);
        Assert.Equal(ChunkEmbeddingStatus.Completed, chunks[1].EmbeddingStatus);
    }

    private static DocumentEmbeddingService CreateService(
        ExtractionTestDocumentRepository repository,
        InMemoryDocumentChunkRepository chunkRepository,
        IEmbeddingProvider provider)
    {
        var aiSettings = new FakeEmbeddingAiSettingsService();
        var factory = new FakeEmbeddingProviderFactory(provider);
        return new DocumentEmbeddingService(
            repository,
            chunkRepository,
            aiSettings,
            factory,
            NullLogger<DocumentEmbeddingService>.Instance);
    }
}

internal sealed class DeterministicEmbeddingProvider : IEmbeddingProvider
{
    public string ProviderName => AiProviderCatalog.Google;

    public string ModelName => "fake-embedding-model";

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (text.Equals("fail-me", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Embedding failed.");
        }

        var hash = text.GetHashCode(StringComparison.Ordinal);
        return Task.FromResult(new[]
        {
            hash / 1000f,
            hash / 2000f,
            hash / 3000f,
            hash / 4000f
        });
    }
}

internal sealed class FakeEmbeddingProviderFactory : IEmbeddingProviderFactory
{
    private readonly IEmbeddingProvider _provider;

    public FakeEmbeddingProviderFactory(IEmbeddingProvider provider) => _provider = provider;

    public IEmbeddingProvider Create(ResolvedEmbeddingProvider settings) => _provider;
}

internal sealed class FakeEmbeddingAiSettingsService : IAiSettingsService
{
    public Task<AiSettingsSnapshot> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task SetSelectedProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task SaveCredentialAsync(
        Guid userId,
        string provider,
        string? apiKey,
        string? chatModel,
        string? endpoint,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<ResolvedAiProvider> ResolveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<ResolvedEmbeddingProvider> ResolveEmbeddingAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ResolvedEmbeddingProvider(AiProviderCatalog.Google, "fake-embedding-model", "test-key"));
}
