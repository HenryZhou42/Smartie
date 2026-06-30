using Microsoft.Extensions.DependencyInjection;
using Smartie.Application.Abstractions;
using Smartie.Application.Automation;
using Smartie.Application.Services;
using Smartie.Domain.Entities;

namespace Smartie.Tests;

public class AutomationServiceTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000401");

    [Fact]
    public async Task CreateAsync_PersistsAutomation()
    {
        var repository = new InMemoryAutomationRepository(UserId);
        var service = CreateService(repository);

        var created = await service.CreateAsync(
            UserId,
            new CreateAutomationRequest(
                "Daily Summary",
                "Morning overview",
                AutomationTriggerType.Scheduled.ToString(),
                AutomationActionType.AskAi.ToString(),
                new AutomationConfig
                {
                    Trigger = new AutomationTriggerConfig { Schedule = "Daily", Time = "08:00" },
                    Action = new AutomationActionConfig { Prompt = "Summarize my day", SaveAsNote = true }
                }.ToJson(),
                true));

        Assert.Equal("Daily Summary", created.Name);
        Assert.Equal(AutomationTriggerType.Scheduled, created.TriggerType);
        Assert.NotNull(created.NextRun);
    }

    [Fact]
    public async Task EnableDisable_UpdatesState()
    {
        var repository = new InMemoryAutomationRepository(UserId);
        var service = CreateService(repository);
        var created = await service.CreateAsync(
            UserId,
            new CreateAutomationRequest("Manual Flow", null, "Manual", "CreateTask", null, false));

        var enabled = await service.EnableAsync(UserId, created.Id);
        Assert.NotNull(enabled);
        Assert.True(enabled!.Enabled);

        var disabled = await service.DisableAsync(UserId, created.Id);
        Assert.NotNull(disabled);
        Assert.False(disabled!.Enabled);
        Assert.Null(disabled.NextRun);
    }

    [Fact]
    public async Task RunNow_CreateTaskAction_Succeeds()
    {
        var repository = new InMemoryAutomationRepository(UserId);
        var taskRepository = new InMemoryTaskRepository(UserId);
        var service = CreateService(repository, taskRepository);

        var created = await service.CreateAsync(
            UserId,
            new CreateAutomationRequest(
                "Create follow-up",
                null,
                AutomationTriggerType.Manual.ToString(),
                AutomationActionType.CreateTask.ToString(),
                new AutomationConfig
                {
                    Action = new AutomationActionConfig { Title = "Follow up", Description = "From automation" }
                }.ToJson(),
                true));

        var result = await service.RunNowAsync(UserId, created.Id);

        Assert.Equal(AutomationRunStatus.Success, result.Status);
        Assert.Contains("Follow up", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAutomation()
    {
        var repository = new InMemoryAutomationRepository(UserId);
        var service = CreateService(repository);
        var created = await service.CreateAsync(
            UserId,
            new CreateAutomationRequest("Temp", null, "Manual", "RunPrompt", null, true));

        var deleted = await service.DeleteAsync(UserId, created.Id);
        Assert.True(deleted);
        Assert.Empty(await service.ListAsync(UserId));
    }

    private static AutomationService CreateService(
        InMemoryAutomationRepository repository,
        InMemoryTaskRepository? taskRepository = null)
    {
        taskRepository ??= new InMemoryTaskRepository(UserId);
        var services = new ServiceCollection()
            .AddSingleton<ITaskRepository>(taskRepository)
            .AddSingleton<ITaskService, TaskService>()
            .AddSingleton<IAutomationEventPublisher>(NoOpAutomationEventPublisher.Instance)
            .AddSingleton<IConversationService, StubConversationService>()
            .AddSingleton<IConversationRepository, StubConversationRepository>()
            .AddSingleton<IDocumentService, StubDocumentService>()
            .AddSingleton<IDocumentExtractionService, StubDocumentExtractionService>()
            .AddSingleton<IDocumentChunkingService, StubDocumentChunkingService>()
            .AddSingleton<IDocumentEmbeddingService, StubDocumentEmbeddingService>()
            .AddSingleton<IMemoryService, StubMemoryService>()
            .BuildServiceProvider();

        var executor = new AutomationActionExecutor(services);

        return new AutomationService(repository, executor);
    }
}

internal sealed class InMemoryAutomationRepository : IAutomationRepository
{
    private readonly Guid _userId;
    private readonly List<AutomationRule> _rules = new();
    private readonly List<AutomationRunLog> _logs = new();

    public InMemoryAutomationRepository(Guid userId) => _userId = userId;

    public Task<IReadOnlyList<AutomationRule>> ListAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AutomationRule>>(_rules.Where(r => r.UserId == userId).OrderByDescending(r => r.UpdatedAt).ToList());

    public Task<AutomationRule?> FindAsync(Guid userId, Guid automationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rules.FirstOrDefault(r => r.UserId == userId && r.Id == automationId));

    public Task<AutomationRule?> FindForUpdateAsync(Guid userId, Guid automationId, CancellationToken cancellationToken = default) =>
        FindAsync(userId, automationId, cancellationToken);

    public async Task<AutomationRule> AddAsync(AutomationRule rule, CancellationToken cancellationToken = default)
    {
        _rules.Add(rule);
        return await Task.FromResult(rule);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> DeleteAsync(Guid userId, Guid automationId, CancellationToken cancellationToken = default)
    {
        var rule = _rules.FirstOrDefault(r => r.UserId == userId && r.Id == automationId);
        if (rule is null)
        {
            return Task.FromResult(false);
        }

        _rules.Remove(rule);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<AutomationRule>> ListDueAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AutomationRule>>(_rules.Where(r => r.Enabled && r.NextRun <= utcNow).ToList());

    public Task<IReadOnlyList<AutomationRule>> ListByTriggerAsync(
        Guid userId,
        AutomationTriggerType triggerType,
        bool enabledOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _rules.Where(r => r.UserId == userId && r.TriggerType == triggerType);
        if (enabledOnly)
        {
            query = query.Where(r => r.Enabled);
        }

        return Task.FromResult<IReadOnlyList<AutomationRule>>(query.ToList());
    }

    public Task<IReadOnlyList<AutomationRunLog>> ListRunLogsAsync(
        Guid userId,
        Guid? automationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _logs.Where(l => _rules.Any(r => r.Id == l.AutomationRuleId && r.UserId == userId));
        if (automationId is not null)
        {
            query = query.Where(l => l.AutomationRuleId == automationId);
        }

        return Task.FromResult<IReadOnlyList<AutomationRunLog>>(query.OrderByDescending(l => l.StartedAt).Take(limit).ToList());
    }

    public async Task AddRunLogAsync(AutomationRunLog log, CancellationToken cancellationToken = default)
    {
        _logs.Add(log);
        await Task.CompletedTask;
    }

    public async Task<AutomationStatsSnapshot> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rules = await ListAsync(userId, cancellationToken);
        var logs = await ListRunLogsAsync(userId, null, 10, cancellationToken);
        return new AutomationStatsSnapshot(
            rules.Count,
            rules.Count(r => r.Enabled),
            rules.Count(r => !r.Enabled),
            rules.Where(r => r.NextRun is not null).Select(r => new AutomationSnapshot(
                r.Id, r.Name, r.Description, r.Enabled, r.TriggerType, r.ActionType, r.ConfigJson,
                r.CreatedAt, r.UpdatedAt, r.LastRun, r.NextRun, r.RunCount)).ToList(),
            logs.Select(l => new AutomationRunLogSnapshot(
                l.Id, l.AutomationRuleId, "Automation", l.Status, l.Message, l.DurationMs, l.StartedAt, l.CompletedAt)).ToList());
    }

    public async Task<AutomationDeveloperStats> GetDeveloperStatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var logs = await ListRunLogsAsync(userId, null, 20, cancellationToken);
        var rules = await ListAsync(userId, cancellationToken);
        var success = logs.Count(l => l.Status == AutomationRunStatus.Success);
        var failed = logs.Count(l => l.Status == AutomationRunStatus.Failed);
        var total = success + failed;
        return new AutomationDeveloperStats(
            rules.Count,
            rules.Count(r => r.Enabled),
            logs.Sum(l => l.DurationMs),
            failed,
            success,
            total == 0 ? 100 : success * 100d / total,
            logs.Select(l => new AutomationRunLogSnapshot(
                l.Id, l.AutomationRuleId, "Automation", l.Status, l.Message, l.DurationMs, l.StartedAt, l.CompletedAt)).ToList());
    }
}

internal sealed class StubConversationService : IConversationService
{
    public Task<IReadOnlyList<Conversation>> ListAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Conversation>>(Array.Empty<Conversation>());

    public Task<Conversation?> GetAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Conversation?>(null);

    public Task<Conversation> CreateAsync(Guid userId, string? title, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Conversation { Id = Guid.NewGuid(), UserId = userId, Title = title ?? "Automation" });

    public Task SetPinnedAsync(Guid conversationId, bool isPinned, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public async IAsyncEnumerable<string> StreamReplyAsync(
        Guid conversationId,
        string userInput,
        IReadOnlyList<Guid>? attachmentDocumentIds = null,
        IReadOnlyList<Guid>? stagingAttachmentIds = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "ok";
        await Task.CompletedTask;
    }

    public Task<IReadOnlyList<Message>> EditUserMessageAsync(
        Guid conversationId,
        Guid messageId,
        string newContent,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());

    public async IAsyncEnumerable<string> StreamRegenerateAsync(
        Guid conversationId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "ok";
        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> StreamEditAndRegenerateAsync(
        Guid conversationId,
        Guid messageId,
        string newContent,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "ok";
        await Task.CompletedTask;
    }

    public Task<Message> SendAsync(Guid conversationId, string userInput, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Message { Id = Guid.NewGuid(), ConversationId = conversationId, Role = MessageRole.Assistant, Content = "Automation reply" });
}

internal sealed class StubConversationRepository : IConversationRepository
{
    public Task<IReadOnlyList<Conversation>> ListAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Conversation>>(Array.Empty<Conversation>());

    public Task<Conversation?> FindAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Conversation?>(null);

    public Task<Conversation> CreateAsync(Guid userId, string title, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Conversation { Id = Guid.NewGuid(), UserId = userId, Title = title });

    public Task<Message> AddMessageAsync(
        Guid conversationId,
        MessageRole role,
        string content,
        CancellationToken cancellationToken = default,
        MessageGenerationStatus generationStatus = MessageGenerationStatus.Complete) =>
        Task.FromResult(new Message { Id = Guid.NewGuid(), ConversationId = conversationId, Role = role, Content = content });

    public Task UpdateTitleAsync(Guid conversationId, string title, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SetPinnedAsync(Guid conversationId, bool isPinned, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Message?> EditUserMessageAndTruncateAsync(
        Guid conversationId,
        Guid messageId,
        string newContent,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Message?>(null);

    public Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}

internal sealed class StubDocumentService : IDocumentService
{
    public Task<IReadOnlyList<Document>> ListAsync(Guid userId, string? search, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());

    public Task<DocumentStats> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DocumentStats(0, 0, 0, 0, 0, 0, null, null, 0, 0, 0, 0));

    public Task<Document?> GetAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Document?>(null);

    public Task<Document> UploadAsync(Guid userId, string originalFileName, Stream content, long sizeBytes, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Document { Id = Guid.NewGuid(), Name = originalFileName });

    public Task<Document?> RenameAsync(Guid userId, Guid documentId, string newName, CancellationToken cancellationToken = default) =>
        Task.FromResult<Document?>(null);

    public Task<bool> DeleteAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<(Document Document, string AbsolutePath)?> GetForOpenAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default) =>
        Task.FromResult<(Document, string)?>(null);

    public KnowledgeBaseSettingsSnapshot GetSettings() =>
        new("Documents", 10_000_000, null, [".txt"], []);
}

internal sealed class StubDocumentExtractionService : IDocumentExtractionService
{
    public Task<Document> ExtractAndPersistAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Document { Id = documentId, Name = "Doc" });
}

internal sealed class StubDocumentChunkingService : IDocumentChunkingService
{
    public Task<Document> ChunkAndPersistAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Document { Id = documentId, Name = "Doc" });

    public Task<Document> RebuildChunksAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Document { Id = documentId, Name = "Doc" });
}

internal sealed class StubDocumentEmbeddingService : IDocumentEmbeddingService
{
    public Task<Document> GenerateAndPersistAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Document { Id = documentId, Name = "Doc" });

    public Task<Document> RebuildEmbeddingsAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Document { Id = documentId, Name = "Doc" });
}

internal sealed class StubMemoryService : IMemoryService
{
    public Task<Memory> StoreMemoryAsync(Guid userId, string content, MemoryCategory category, MemoryImportance importance, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Memory { Id = Guid.NewGuid(), Content = content });

    public Task<IReadOnlyList<MemorySearchResult>> SearchMemoryAsync(Guid userId, string query, int topK, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MemorySearchResult>>(Array.Empty<MemorySearchResult>());

    public Task<bool> DeleteMemoryAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<Memory?> PinMemoryAsync(Guid userId, Guid memoryId, bool pinned, CancellationToken cancellationToken = default) =>
        Task.FromResult<Memory?>(null);

    public Task<Memory?> UpdateMemoryAsync(Guid userId, Guid memoryId, string content, MemoryCategory category, MemoryImportance importance, CancellationToken cancellationToken = default) =>
        Task.FromResult<Memory?>(null);

    public Task<IReadOnlyList<Memory>> ListMemoriesAsync(Guid userId, MemoryCategory? category, bool? pinnedOnly, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Memory>>(Array.Empty<Memory>());

    public Task ExtractAndStoreFromUserMessageAsync(Guid userId, string userMessage, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<MemorySettingsSnapshot> GetSettingsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new MemorySettingsSnapshot(true, 200, 365, 0));

    public Task UpdateSettingsAsync(Guid userId, MemorySettingsUpdate update, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<MemoryDeveloperStats> GetDeveloperStatsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new MemoryDeveloperStats(0, 0, null, 5, 50));
}
