using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Smartie.Application.Configuration;
using Smartie.Application.Services;
using Smartie.Domain.Entities;
using Smartie.Infrastructure.Documents;
using Smartie.Infrastructure.Storage;

namespace Smartie.Tests;

public class AttachedDocumentPromptBuilderTests
{
    [Fact]
    public async Task BuildAugmentedUserMessageAsync_IncludesDocumentContentAndQuestion()
    {
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var root = Path.Combine(Path.GetTempPath(), "smartie-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var relativePath = $"{documentId:N}/policy.txt";
            var absoluteDirectory = Path.Combine(root, documentId.ToString("N"));
            Directory.CreateDirectory(absoluteDirectory);
            await File.WriteAllTextAsync(Path.Combine(absoluteDirectory, "policy.txt"), "You get 20 vacation days.");

            var document = new Document
            {
                Id = documentId,
                UserId = userId,
                Name = "EmployeeContract.txt",
                FileName = "policy.txt",
                Extension = "txt",
                RelativePath = relativePath
            };

            var repository = new InMemoryDocumentRepository(document);
            var storage = new TestDocumentStorage(root);
            var builder = CreateBuilder(repository, storage);

            var prompt = await builder.BuildAugmentedUserMessageAsync(
                userId,
                "How many vacation days does this contract mention?",
                [CreateKnowledgeBaseAttachment(documentId, document)]);

            Assert.Contains("EmployeeContract.txt", prompt);
            Assert.Contains("You get 20 vacation days.", prompt);
            Assert.Contains("How many vacation days does this contract mention?", prompt);
            Assert.Contains("Knowledge Base", prompt);
            Assert.Contains("Do not invent facts from the documents.", prompt);
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
    public async Task BuildAugmentedUserMessageAsync_ExtractsMarkdownWhenExtensionStoredWithoutDot()
    {
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var root = Path.Combine(Path.GetTempPath(), "smartie-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var relativePath = $"{documentId:N}/policy.md";
            var absoluteDirectory = Path.Combine(root, documentId.ToString("N"));
            Directory.CreateDirectory(absoluteDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(absoluteDirectory, "policy.md"),
                "Employees receive 15 vacation days during the first 3 years.");

            var document = new Document
            {
                Id = documentId,
                UserId = userId,
                Name = "Smartie_Test_Document",
                FileName = "policy.md",
                Extension = "md",
                RelativePath = relativePath
            };

            var repository = new InMemoryDocumentRepository(document);
            var storage = new TestDocumentStorage(root);
            var builder = CreateBuilder(repository, storage);

            var prompt = await builder.BuildAugmentedUserMessageAsync(
                userId,
                "How many vacation days does a new employee receive?",
                [CreateKnowledgeBaseAttachment(documentId, document)]);

            Assert.Contains("15 vacation days during the first 3 years", prompt);
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
    public async Task BuildAugmentedUserMessageAsync_IncludesLocalUploadContent()
    {
        var userId = Guid.NewGuid();
        var root = Path.Combine(Path.GetTempPath(), "smartie-tests", Guid.NewGuid().ToString("N"));
        var fileName = "notes.txt";
        var absolutePath = Path.Combine(root, fileName);
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(absolutePath, "Remote work is allowed on Fridays.");

        try
        {
            var repository = new InMemoryDocumentRepository();
            var storage = new TestDocumentStorage(root);
            var chatStorage = new TestChatAttachmentStorage(root);
            var builder = CreateBuilder(repository, storage, chatStorage);

            var prompt = await builder.BuildAugmentedUserMessageAsync(
                userId,
                "What does the note say about remote work?",
                [new MessageAttachment
                {
                    SourceType = MessageAttachmentSourceType.LocalUpload,
                    OriginalFileName = fileName,
                    StoredFileName = fileName,
                    FilePath = fileName,
                    Extension = "txt"
                }]);

            Assert.Contains("Local Upload", prompt);
            Assert.Contains("Remote work is allowed on Fridays.", prompt);
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
    public async Task BuildAugmentedUserMessageAsync_TruncatesWhenOverLimit()
    {
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var root = Path.Combine(Path.GetTempPath(), "smartie-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var relativePath = $"{documentId:N}/large.txt";
            var absoluteDirectory = Path.Combine(root, documentId.ToString("N"));
            Directory.CreateDirectory(absoluteDirectory);
            await File.WriteAllTextAsync(Path.Combine(absoluteDirectory, "large.txt"), new string('x', 500));

            var document = new Document
            {
                Id = documentId,
                UserId = userId,
                Name = "Large.txt",
                FileName = "large.txt",
                Extension = "txt",
                RelativePath = relativePath
            };

            var repository = new InMemoryDocumentRepository(document);
            var storage = new TestDocumentStorage(root);
            var builder = new AttachedDocumentPromptBuilder(
                repository,
                new TxtDocumentTextExtractor(storage),
                new ChatFileTextExtractor(),
                new TestChatAttachmentStorage(root),
                Options.Create(new AttachedDocumentContextOptions { MaxTotalCharacters = 100 }),
                NullLogger<AttachedDocumentPromptBuilder>.Instance);

            var prompt = await builder.BuildAugmentedUserMessageAsync(
                userId,
                "Summarize this.",
                [CreateKnowledgeBaseAttachment(documentId, document)]);

            Assert.Contains("[Some attached content was truncated due to context limit.]", prompt);
            Assert.DoesNotContain(new string('x', 500), prompt);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static AttachedDocumentPromptBuilder CreateBuilder(
        InMemoryDocumentRepository repository,
        TestDocumentStorage storage,
        TestChatAttachmentStorage? chatStorage = null) =>
        new(
            repository,
            new CompositeDocumentTextExtractor([
                new TxtDocumentTextExtractor(storage),
                new MarkdownDocumentTextExtractor(storage)
            ]),
            new ChatFileTextExtractor(),
            chatStorage ?? new TestChatAttachmentStorage(storage.GetStorageRoot()),
            Options.Create(new AttachedDocumentContextOptions { MaxTotalCharacters = 30_000 }),
            NullLogger<AttachedDocumentPromptBuilder>.Instance);

    private static MessageAttachment CreateKnowledgeBaseAttachment(Guid documentId, Document? document = null) =>
        new()
        {
            DocumentId = documentId,
            SourceType = MessageAttachmentSourceType.KnowledgeBase,
            Document = document
        };
}

internal sealed class InMemoryDocumentRepository : Smartie.Application.Abstractions.IDocumentRepository
{
    private readonly Document? _document;

    public InMemoryDocumentRepository(Document? document = null) => _document = document;

    public Task<IReadOnlyList<Document>> ListAsync(Guid userId, string? search, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Document>>(_document is null ? Array.Empty<Document>() : [_document]);

    public Task<Document?> FindAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_document is not null && documentId == _document.Id && userId == _document.UserId ? _document : null);

    public Task<Smartie.Application.Abstractions.DocumentStats> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Document> AddAsync(Document document, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Document?> UpdateNameAsync(Guid documentId, Guid userId, string name, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<bool> DeleteAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Document?> FindForUpdateAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        FindAsync(documentId, userId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class TestDocumentStorage : Smartie.Application.Abstractions.IDocumentStorage
{
    private readonly string _root;

    public TestDocumentStorage(string root) => _root = root;

    public Task<string> SaveAsync(Guid documentId, string fileName, Stream content, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public string GetAbsolutePath(string relativePath) =>
        Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public bool Exists(string relativePath) => File.Exists(GetAbsolutePath(relativePath));

    public string GetStorageRoot() => _root;
}

internal sealed class TestChatAttachmentStorage : Smartie.Application.Abstractions.IChatAttachmentStorage
{
    private readonly string _root;

    public TestChatAttachmentStorage(string root) => _root = root;

    public Task<Smartie.Application.Abstractions.ChatAttachmentFileInfo> SaveStagingAsync(
        Guid conversationId,
        Guid stagingId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Smartie.Application.Abstractions.ChatAttachmentFileInfo>> CommitStagingAsync(
        Guid conversationId,
        Guid stagingId,
        Guid messageId,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task DeleteStagingAsync(
        Guid conversationId,
        Guid stagingId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public string GetAbsolutePath(string relativePath) =>
        Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public bool StagingExists(Guid conversationId, Guid stagingId) => false;
}
