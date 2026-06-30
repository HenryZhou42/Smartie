using Microsoft.Extensions.Logging.Abstractions;
using Smartie.Application.Abstractions;
using Smartie.Application.Services;
using Smartie.Domain.Entities;
using Smartie.Infrastructure.Documents;
using Smartie.Infrastructure.Storage;

namespace Smartie.Tests;

public class DocumentExtractionServiceTests
{
    public DocumentExtractionServiceTests() => TestDocumentFixtures.EnsureFixtures();

    [Theory]
    [InlineData("Smartie_Test_Document.md", "MarkdownDocumentTextExtractor", "15 days", "Sarah Johnson")]
    [InlineData("CompanyPolicy.md", "MarkdownDocumentTextExtractor", "15 vacation days", "Sarah Johnson")]
    [InlineData("Resume.docx", "DocxDocumentTextExtractor", "25 vacation days", "Sarah Johnson")]
    [InlineData("Sample.pdf", "PdfDocumentTextExtractor", "15 vacation days", "Sarah Johnson")]
    public async Task ExtractAndPersistAsync_ExtractsSupportedFormats(
        string fileName,
        string expectedExtractor,
        string expectedPhrase,
        string expectedPerson)
    {
        var fixturePath = TestDocumentFixtures.GetTestDataPath(fileName);
        Assert.True(File.Exists(fixturePath), $"Missing fixture: {fixturePath}");

        var root = Path.Combine(Path.GetTempPath(), "smartie-extraction-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
            var storedFileName = Path.GetFileName(fileName);
            var relativePath = $"{documentId:N}/{storedFileName}";
            var absoluteDir = Path.Combine(root, documentId.ToString("N"));
            Directory.CreateDirectory(absoluteDir);
            File.Copy(fixturePath, Path.Combine(absoluteDir, storedFileName));

            var storage = new TestDocumentStorage(root);
            var repository = new ExtractionTestDocumentRepository(new Document
            {
                Id = documentId,
                UserId = userId,
                Name = Path.GetFileNameWithoutExtension(fileName),
                FileName = storedFileName,
                Extension = extension,
                RelativePath = relativePath,
                SizeBytes = new FileInfo(fixturePath).Length
            });

            var service = new DocumentExtractionService(
                repository,
                new DocumentTextExtractionRouter(
                    new TxtDocumentTextExtractor(storage),
                    new MarkdownDocumentTextExtractor(storage),
                    new PdfDocumentTextExtractor(storage),
                    new DocxDocumentTextExtractor(storage)),
                new NoOpDocumentChunkingService(repository),
                NullLogger<DocumentExtractionService>.Instance);

            var result = await service.ExtractAndPersistAsync(documentId, userId);

            Assert.Equal(DocumentExtractionStatus.Completed, result.ExtractionStatus);
            Assert.Equal(expectedExtractor, result.ExtractorUsed);
            Assert.Contains(expectedPhrase, result.ExtractedText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(expectedPerson, result.ExtractedText, StringComparison.OrdinalIgnoreCase);
            Assert.True(result.ExtractedLength > 0);
            Assert.NotNull(result.ExtractedAt);
            Assert.True(result.ExtractionDurationMs >= 0);

            var reloaded = await repository.FindAsync(documentId, userId);
            Assert.NotNull(reloaded?.ExtractedText);
            Assert.Equal(DocumentExtractionStatus.Completed, reloaded!.ExtractionStatus);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExtractAndPersistAsync_Txt_ReadsPlainText()
    {
        var root = Path.Combine(Path.GetTempPath(), "smartie-extraction-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var relativePath = $"{documentId:N}/notes.txt";
            var absoluteDir = Path.Combine(root, documentId.ToString("N"));
            Directory.CreateDirectory(absoluteDir);
            await File.WriteAllTextAsync(Path.Combine(absoluteDir, "notes.txt"), "Plain text notes for Smartie.");

            var storage = new TestDocumentStorage(root);
            var repository = new ExtractionTestDocumentRepository(new Document
            {
                Id = documentId,
                UserId = userId,
                Name = "notes",
                FileName = "notes.txt",
                Extension = "txt",
                RelativePath = relativePath,
                SizeBytes = 32
            });

            var service = new DocumentExtractionService(
                repository,
                new DocumentTextExtractionRouter(
                    new TxtDocumentTextExtractor(storage),
                    new MarkdownDocumentTextExtractor(storage),
                    new PdfDocumentTextExtractor(storage),
                    new DocxDocumentTextExtractor(storage)),
                new NoOpDocumentChunkingService(repository),
                NullLogger<DocumentExtractionService>.Instance);

            var result = await service.ExtractAndPersistAsync(documentId, userId);

            Assert.Equal(DocumentExtractionStatus.Completed, result.ExtractionStatus);
            Assert.Equal(TxtDocumentTextExtractor.ExtractorName, result.ExtractorUsed);
            Assert.Contains("Plain text notes", result.ExtractedText);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

internal sealed class ExtractionTestDocumentRepository : Smartie.Application.Abstractions.IDocumentRepository
{
    private Document _document;

    public ExtractionTestDocumentRepository(Document document) => _document = document;

    public Task<IReadOnlyList<Document>> ListAsync(Guid userId, string? search, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Document>>([_document]);

    public Task<Document?> FindAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(documentId == _document.Id && userId == _document.UserId ? Clone(_document) : null);

    public Task<Document?> FindForUpdateAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(documentId == _document.Id && userId == _document.UserId ? _document : null);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<Smartie.Application.Abstractions.DocumentStats> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Document> AddAsync(Document document, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Document?> UpdateNameAsync(Guid documentId, Guid userId, string name, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<bool> DeleteAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    private static Document Clone(Document source) =>
        new()
        {
            Id = source.Id,
            UserId = source.UserId,
            Name = source.Name,
            FileName = source.FileName,
            Extension = source.Extension,
            RelativePath = source.RelativePath,
            SizeBytes = source.SizeBytes,
            ExtractedText = source.ExtractedText,
            ExtractedLength = source.ExtractedLength,
            ExtractedAt = source.ExtractedAt,
            ExtractionStatus = source.ExtractionStatus,
            ExtractorUsed = source.ExtractorUsed,
            ExtractionDurationMs = source.ExtractionDurationMs,
            ExtractionError = source.ExtractionError,
            IsChunked = source.IsChunked,
            ChunkCount = source.ChunkCount,
            ChunkedAt = source.ChunkedAt
        };
}

internal sealed class NoOpDocumentChunkingService : Smartie.Application.Abstractions.IDocumentChunkingService
{
    private readonly ExtractionTestDocumentRepository _repository;

    public NoOpDocumentChunkingService(ExtractionTestDocumentRepository repository) =>
        _repository = repository;

    public async Task<Document> ChunkAndPersistAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        (await _repository.FindForUpdateAsync(documentId, userId, cancellationToken).ConfigureAwait(false))!;

    public Task<Document> RebuildChunksAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        ChunkAndPersistAsync(documentId, userId, cancellationToken);
}

internal sealed class NoOpDocumentEmbeddingService : IDocumentEmbeddingService
{
    private readonly ExtractionTestDocumentRepository _repository;

    public NoOpDocumentEmbeddingService(ExtractionTestDocumentRepository repository) =>
        _repository = repository;

    public async Task<Document> GenerateAndPersistAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        (await _repository.FindForUpdateAsync(documentId, userId, cancellationToken).ConfigureAwait(false))!;

    public Task<Document> RebuildEmbeddingsAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        GenerateAndPersistAsync(documentId, userId, cancellationToken);
}
