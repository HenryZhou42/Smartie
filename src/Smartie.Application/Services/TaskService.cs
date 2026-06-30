using Smartie.Application.Automation;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public sealed class TaskService : ITaskService
{
    private readonly ITaskRepository _repository;
    private readonly IAutomationEventPublisher _automations;

    public TaskService(ITaskRepository repository, IAutomationEventPublisher automations)
    {
        _repository = repository;
        _automations = automations;
    }

    public async Task<IReadOnlyList<TaskItem>> ListAsync(
        Guid userId,
        TaskViewFilter view,
        string? search,
        TaskSortOption? sort,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsInternalAsync(userId, cancellationToken).ConfigureAwait(false);
        var effectiveSort = sort ?? settings.DefaultSort;
        return await _repository
            .ListAsync(userId, view, search, effectiveSort, settings.ShowCompleted, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TaskItem> CreateAsync(
        Guid userId,
        string title,
        string? description,
        TaskPriority? priority,
        SmartieTaskStatus? status,
        DateTimeOffset? dueDate,
        CancellationToken cancellationToken = default)
    {
        var trimmed = title.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Task title must not be empty.", nameof(title));
        }

        var settings = await GetSettingsInternalAsync(userId, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var effectiveStatus = status ?? SmartieTaskStatus.Pending;
        var task = new TaskItem
        {
            UserId = userId,
            Title = trimmed,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Priority = priority ?? settings.DefaultPriority,
            Status = effectiveStatus,
            DueDate = dueDate,
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = effectiveStatus == SmartieTaskStatus.Completed ? now : null
        };

        return await _repository.AddAsync(task, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TaskItem?> UpdateAsync(
        Guid userId,
        Guid taskId,
        string title,
        string? description,
        TaskPriority priority,
        SmartieTaskStatus status,
        DateTimeOffset? dueDate,
        bool pinned,
        bool archived,
        CancellationToken cancellationToken = default)
    {
        var trimmed = title.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Task title must not be empty.", nameof(title));
        }

        var task = await _repository.FindForUpdateAsync(userId, taskId, cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        task.Title = trimmed;
        task.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        task.Priority = priority;
        task.Status = status;
        task.DueDate = dueDate;
        task.Pinned = pinned;
        task.Archived = archived;
        task.UpdatedAt = now;
        task.CompletedAt = status == SmartieTaskStatus.Completed
            ? task.CompletedAt ?? now
            : null;

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return task;
    }

    public async Task<TaskItem?> CompleteAsync(
        Guid userId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await _repository.FindForUpdateAsync(userId, taskId, cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        task.Status = SmartieTaskStatus.Completed;
        task.CompletedAt = now;
        task.UpdatedAt = now;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _automations.PublishAsync(
            userId,
            AutomationTriggerType.TaskCompleted,
            new AutomationEventContext(
                TaskPriority: task.Priority.ToString(),
                EventDate: now,
                TaskId: task.Id),
            cancellationToken).ConfigureAwait(false);

        return task;
    }

    public async Task<TaskItem?> PinAsync(
        Guid userId,
        Guid taskId,
        bool pinned,
        CancellationToken cancellationToken = default)
    {
        var task = await _repository.FindForUpdateAsync(userId, taskId, cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            return null;
        }

        task.Pinned = pinned;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return task;
    }

    public async Task<TaskItem?> ArchiveAsync(
        Guid userId,
        Guid taskId,
        bool archived,
        CancellationToken cancellationToken = default)
    {
        var task = await _repository.FindForUpdateAsync(userId, taskId, cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            return null;
        }

        task.Archived = archived;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return task;
    }

    public Task<bool> DeleteAsync(
        Guid userId,
        Guid taskId,
        CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(userId, taskId, cancellationToken);

    public Task<TaskStatsSnapshot> GetStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _repository.GetStatsAsync(userId, cancellationToken);

    public async Task<TaskSettingsSnapshot> GetSettingsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsInternalAsync(userId, cancellationToken).ConfigureAwait(false);
        return new TaskSettingsSnapshot(settings.DefaultSort, settings.DefaultPriority, settings.ShowCompleted);
    }

    public async Task UpdateSettingsAsync(
        Guid userId,
        TaskSettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserForUpdateAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"User {userId} was not found.");

        if (update.DefaultSort is TaskSortOption sort)
        {
            user.TaskDefaultSort = sort;
        }

        if (update.DefaultPriority is TaskPriority priority)
        {
            user.TaskDefaultPriority = priority;
        }

        if (update.ShowCompleted is bool showCompleted)
        {
            user.TaskShowCompleted = showCompleted;
        }

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TaskDeveloperStats> GetDeveloperStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var stats = await _repository.GetStatsAsync(userId, cancellationToken).ConfigureAwait(false);
        return new TaskDeveloperStats(stats.TotalCount, stats.CompletedCount, 0);
    }

    private async Task<(TaskSortOption DefaultSort, TaskPriority DefaultPriority, bool ShowCompleted)> GetSettingsInternalAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _repository.GetUserSettingsAsync(userId, cancellationToken).ConfigureAwait(false);
        return (
            user?.TaskDefaultSort ?? TaskSortOption.DueDate,
            user?.TaskDefaultPriority ?? TaskPriority.Medium,
            user?.TaskShowCompleted ?? true);
    }
}
