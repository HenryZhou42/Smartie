using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Smartie.Application.Abstractions;
using Smartie.Application.Services;
using Smartie.Domain.Entities;

namespace Smartie.Tests;

public class ConversationServiceTests
{
    private static ConversationService CreateService(
        out InMemoryConversationRepository repository,
        IChatAiService ai,
        IAttachedDocumentPromptBuilder? promptBuilder = null,
        InMemoryMessageAttachmentRepository? attachments = null,
        IChatAttachmentStorage? chatAttachmentStorage = null)
    {
        repository = new InMemoryConversationRepository();
        attachments ??= new InMemoryMessageAttachmentRepository();
        promptBuilder ??= new PassthroughAttachedDocumentPromptBuilder();
        chatAttachmentStorage ??= new FakeChatAttachmentStorage();
        return new ConversationService(
            repository,
            attachments,
            chatAttachmentStorage,
            promptBuilder,
            ai,
            NullLogger<ConversationService>.Instance);
    }

    [Fact]
    public async Task StreamReplyAsync_PersistsUserAndAssistantMessages()
    {
        var ai = new FakeChatAiService("Hello", ", ", "world");
        var service = CreateService(out var repository, ai);
        var conversation = await service.CreateAsync(Guid.NewGuid(), null);

        var chunks = new List<string>();
        await foreach (var chunk in service.StreamReplyAsync(conversation.Id, "Hi there"))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(new[] { "Hello", ", ", "world" }, chunks);

        var stored = await repository.FindAsync(conversation.Id);
        Assert.NotNull(stored);
        Assert.Collection(stored!.Messages,
            m => Assert.Equal(MessageRole.User, m.Role),
            m =>
            {
                Assert.Equal(MessageRole.Assistant, m.Role);
                Assert.Equal("Hello, world", m.Content);
            });
    }

    [Fact]
    public async Task StreamReplyAsync_SetsTitleFromFirstUserMessage()
    {
        var ai = new FakeChatAiService("ok");
        var service = CreateService(out var repository, ai);
        var conversation = await service.CreateAsync(Guid.NewGuid(), null);

        await foreach (var _ in service.StreamReplyAsync(conversation.Id, "Plan my week please"))
        {
        }

        var stored = await repository.FindAsync(conversation.Id);
        Assert.Equal("Plan my week please", stored!.Title);
    }

    [Fact]
    public async Task StreamReplyAsync_IncludesPriorHistoryForTheModel()
    {
        var ai = new FakeChatAiService("reply");
        var service = CreateService(out _, ai);
        var conversation = await service.CreateAsync(Guid.NewGuid(), "Existing");

        await foreach (var _ in service.StreamReplyAsync(conversation.Id, "first"))
        {
        }

        Assert.NotNull(ai.LastHistory);
        Assert.Single(ai.LastHistory!);
        Assert.Equal(MessageRole.User, ai.LastHistory![0].Role);
        Assert.Equal("first", ai.LastHistory[0].Content);
    }

    [Fact]
    public async Task StreamReplyAsync_Throws_WhenConversationMissing()
    {
        var service = CreateService(out _, new FakeChatAiService("x"));

        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
        {
            await foreach (var _ in service.StreamReplyAsync(Guid.NewGuid(), "hello"))
            {
            }
        });
    }

    [Fact]
    public async Task StreamReplyAsync_PersistsPartialReplyAsStopped_WhenCancelled()
    {
        var ai = new SlowFakeChatAiService("Hello", " world");
        var service = CreateService(out var repository, ai);
        var conversation = await service.CreateAsync(Guid.NewGuid(), null);
        using var cts = new CancellationTokenSource();

        var streamTask = Task.Run(async () =>
        {
            var chunks = new List<string>();
            await foreach (var chunk in service.StreamReplyAsync(conversation.Id, "Hi", cancellationToken: cts.Token))
            {
                chunks.Add(chunk);
            }

            return chunks;
        });

        await Task.Delay(120);
        cts.Cancel();

        var chunks = await streamTask;

        Assert.NotEmpty(chunks);

        var stored = await repository.FindAsync(conversation.Id);
        Assert.NotNull(stored);
        var assistant = Assert.Single(stored!.Messages, m => m.Role == MessageRole.Assistant);
        Assert.Equal(MessageGenerationStatus.Stopped, assistant.GenerationStatus);
        Assert.Equal("Hello", assistant.Content);
    }

    [Fact]
    public async Task EditUserMessageAsync_TruncatesWithoutGeneratingReply()
    {
        var service = CreateService(out var repository, new FakeChatAiService("unused"));
        var conversation = await service.CreateAsync(Guid.NewGuid(), null);

        await foreach (var _ in service.StreamReplyAsync(conversation.Id, "hello"))
        {
        }

        var stored = await repository.FindAsync(conversation.Id);
        var userMessage = stored!.Messages.First(m => m.Role == MessageRole.User);

        var messages = await service.EditUserMessageAsync(conversation.Id, userMessage.Id, "hello again");

        Assert.Collection(messages,
            m =>
            {
                Assert.Equal(MessageRole.User, m.Role);
                Assert.Equal("hello again", m.Content);
                Assert.True(m.IsEdited);
            });

        var reloaded = await repository.FindAsync(conversation.Id);
        Assert.Single(reloaded!.Messages);
    }

    [Fact]
    public async Task StreamEditAndRegenerateAsync_TruncatesAndStreamsNewReply()
    {
        var ai = new FakeChatAiService("new", " reply");
        var service = CreateService(out var repository, ai);
        var conversation = await service.CreateAsync(Guid.NewGuid(), null);

        await foreach (var _ in service.StreamReplyAsync(conversation.Id, "hello"))
        {
        }

        var stored = await repository.FindAsync(conversation.Id);
        var userMessage = stored!.Messages.First(m => m.Role == MessageRole.User);

        var chunks = new List<string>();
        await foreach (var chunk in service.StreamEditAndRegenerateAsync(conversation.Id, userMessage.Id, "hello again"))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(new[] { "new", " reply" }, chunks);

        var reloaded = await repository.FindAsync(conversation.Id);
        Assert.Collection(reloaded!.Messages,
            m =>
            {
                Assert.Equal(MessageRole.User, m.Role);
                Assert.Equal("hello again", m.Content);
                Assert.True(m.IsEdited);
            },
            m =>
            {
                Assert.Equal(MessageRole.Assistant, m.Role);
                Assert.Equal("new reply", m.Content);
            });
    }

    [Fact]
    public async Task StreamReplyAsync_WithAttachments_AugmentsPromptForModel()
    {
        var documentId = Guid.NewGuid();
        var promptBuilder = new RecordingAttachedDocumentPromptBuilder();
        var ai = new FakeChatAiService("answer");
        var service = CreateService(out _, ai, promptBuilder);
        var conversation = await service.CreateAsync(Guid.NewGuid(), null);

        await foreach (var _ in service.StreamReplyAsync(
                           conversation.Id,
                           "How many vacation days?",
                           [documentId]))
        {
        }

        Assert.NotNull(promptBuilder.LastAttachments);
        Assert.Single(promptBuilder.LastAttachments!);
        Assert.Equal(documentId, promptBuilder.LastAttachments![0].DocumentId);
        Assert.Equal(MessageAttachmentSourceType.KnowledgeBase, promptBuilder.LastAttachments[0].SourceType);
        Assert.Equal("How many vacation days?", promptBuilder.LastUserMessage);
        Assert.Contains("AUGMENTED", promptBuilder.LastAugmentedMessage);

        Assert.NotNull(ai.LastHistory);
        var lastUser = Assert.Single(ai.LastHistory!, m => m.Role == MessageRole.User);
        Assert.Contains("AUGMENTED", lastUser.Content);
        Assert.Equal("How many vacation days?", promptBuilder.LastUserMessage);
    }

    [Fact]
    public async Task SendAsync_ReturnsAggregatedAssistantMessage()
    {
        var ai = new FakeChatAiService("a", "b", "c");
        var service = CreateService(out _, ai);
        var conversation = await service.CreateAsync(Guid.NewGuid(), null);

        var reply = await service.SendAsync(conversation.Id, "hi");

        Assert.Equal(MessageRole.Assistant, reply.Role);
        Assert.Equal("abc", reply.Content);
    }
}

/// <summary>Yields chunks slowly so cancellation can interrupt mid-stream.</summary>
internal sealed class SlowFakeChatAiService : IChatAiService
{
    private readonly string[] _chunks;

    public SlowFakeChatAiService(params string[] chunks) => _chunks = chunks;

    public async IAsyncEnumerable<string> StreamReplyAsync(
        IReadOnlyList<Message> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var chunk in _chunks)
        {
            await Task.Delay(100, cancellationToken);
            yield return chunk;
        }
    }
}
