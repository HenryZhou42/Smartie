using Smartie.Application.Abstractions;
using Smartie.Application.Services;
using Smartie.Application.Automation;
using Smartie.Domain.Entities;

namespace Smartie.Tests;

public class TaskServiceTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000301");

    [Fact]
    public async Task CreateAsync_PersistsTask()
    {
        var repository = new InMemoryTaskRepository(UserId);
        var service = new TaskService(repository, NoOpAutomationEventPublisher.Instance);

        var task = await service.CreateAsync(UserId, "Build Smartie RAG", "Implement semantic retrieval.", null, null, null);

        Assert.Equal("Build Smartie RAG", task.Title);
        Assert.Equal(SmartieTaskStatus.Pending, task.Status);
        Assert.Equal(TaskPriority.Medium, task.Priority);
    }

    [Fact]
    public async Task CompleteAsync_SetsCompletedStatus()
    {
        var repository = new InMemoryTaskRepository(UserId);
        var service = new TaskService(repository, NoOpAutomationEventPublisher.Instance);
        var created = await service.CreateAsync(UserId, "Finish docs", null, null, null, null);

        var completed = await service.CompleteAsync(UserId, created.Id);

        Assert.NotNull(completed);
        Assert.Equal(SmartieTaskStatus.Completed, completed!.Status);
        Assert.NotNull(completed.CompletedAt);
    }

    [Fact]
    public async Task ListAsync_SearchFiltersTasks()
    {
        var repository = new InMemoryTaskRepository(UserId);
        var service = new TaskService(repository, NoOpAutomationEventPublisher.Instance);
        await service.CreateAsync(UserId, "Build Smartie RAG", "Implement semantic retrieval.", TaskPriority.High, SmartieTaskStatus.InProgress, DateTimeOffset.UtcNow.AddDays(1));
        await service.CreateAsync(UserId, "Buy milk", null, null, null, null);

        var results = await service.ListAsync(UserId, TaskViewFilter.All, "smartie", null);

        Assert.Single(results);
        Assert.Contains("Smartie", results[0].Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ChangesDefaults()
    {
        var repository = new InMemoryTaskRepository(UserId);
        var service = new TaskService(repository, NoOpAutomationEventPublisher.Instance);

        await service.UpdateSettingsAsync(
            UserId,
            new TaskSettingsUpdate(TaskSortOption.Title, TaskPriority.Critical, false));

        var settings = await service.GetSettingsAsync(UserId);
        Assert.Equal(TaskSortOption.Title, settings.DefaultSort);
        Assert.Equal(TaskPriority.Critical, settings.DefaultPriority);
        Assert.False(settings.ShowCompleted);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTask()
    {
        var repository = new InMemoryTaskRepository(UserId);
        var service = new TaskService(repository, NoOpAutomationEventPublisher.Instance);
        var created = await service.CreateAsync(UserId, "Temporary", null, null, null, null);

        var deleted = await service.DeleteAsync(UserId, created.Id);

        Assert.True(deleted);
        Assert.Empty(await service.ListAsync(UserId, TaskViewFilter.All, null, null));
    }
}

internal sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly Dictionary<Guid, TaskItem> _tasks = new();
    private User _user;

    public InMemoryTaskRepository(Guid userId)
    {
        _user = new User
        {
            Id = userId,
            DisplayName = "Test User",
            TaskDefaultSort = TaskSortOption.DueDate,
            TaskDefaultPriority = TaskPriority.Medium,
            TaskShowCompleted = true
        };
    }

    public Task<IReadOnlyList<TaskItem>> ListAsync(
        Guid userId,
        TaskViewFilter view,
        string? search,
        TaskSortOption sort,
        bool showCompleted,
        CancellationToken cancellationToken = default)
    {
        var tasks = _tasks.Values.Where(t => t.UserId == userId).ToList();
        var utcNow = DateTimeOffset.UtcNow;
        var filtered = TaskQueryHelper.ApplyViewFilter(tasks, view, utcNow);
        filtered = TaskQueryHelper.ApplySearch(filtered, search);
        filtered = TaskQueryHelper.ApplyCompletedVisibility(filtered, showCompleted, view);
        return Task.FromResult<IReadOnlyList<TaskItem>>(TaskQueryHelper.ApplySort(filtered, sort).ToList());
    }

    public Task<TaskItem?> FindAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_tasks.TryGetValue(taskId, out var task) && task.UserId == userId ? task : null);

    public Task<TaskItem?> FindForUpdateAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default) =>
        FindAsync(userId, taskId, cancellationToken);

    public Task<TaskItem> AddAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        _tasks[task.Id] = task;
        return Task.FromResult(task);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(taskId, out var task) || task.UserId != userId)
        {
            return Task.FromResult(false);
        }

        _tasks.Remove(taskId);
        return Task.FromResult(true);
    }

    public Task<TaskStatsSnapshot> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tasks = _tasks.Values.Where(t => t.UserId == userId).ToList();
        var utcNow = DateTimeOffset.UtcNow;
        var today = utcNow.Date;
        var active = tasks.Where(t => !t.Archived).ToList();
        var recent = active.OrderByDescending(t => t.UpdatedAt).Take(5).ToList();
        return Task.FromResult(new TaskStatsSnapshot(
            tasks.Count,
            active.Count(t => t.Status is SmartieTaskStatus.Pending or SmartieTaskStatus.InProgress),
            active.Count(t => t.Status == SmartieTaskStatus.Completed),
            active.Count(t => t.DueDate is { } due && due.UtcDateTime.Date == today && t.Status is not SmartieTaskStatus.Completed and not SmartieTaskStatus.Cancelled),
            active.Count(t => t.Pinned),
            tasks.Count(t => t.Archived),
            recent));
    }

    public Task<User?> GetUserSettingsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<User?>(_user.Id == userId ? _user : null);

    public Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<User?>(_user.Id == userId ? _user : null);
}

internal sealed class NoOpAutomationEventPublisher : IAutomationEventPublisher
{
    public static NoOpAutomationEventPublisher Instance { get; } = new();

    public Task PublishAsync(
        Guid userId,
        AutomationTriggerType triggerType,
        AutomationEventContext context,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
