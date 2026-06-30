using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface ITaskService
{
    Task<IReadOnlyList<TaskItem>> ListAsync(
        Guid userId,
        TaskViewFilter view,
        string? search,
        TaskSortOption? sort,
        CancellationToken cancellationToken = default);

    Task<TaskItem> CreateAsync(
        Guid userId,
        string title,
        string? description,
        TaskPriority? priority,
        SmartieTaskStatus? status,
        DateTimeOffset? dueDate,
        CancellationToken cancellationToken = default);

    Task<TaskItem?> UpdateAsync(
        Guid userId,
        Guid taskId,
        string title,
        string? description,
        TaskPriority priority,
        SmartieTaskStatus status,
        DateTimeOffset? dueDate,
        bool pinned,
        bool archived,
        CancellationToken cancellationToken = default);

    Task<TaskItem?> CompleteAsync(
        Guid userId,
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<TaskItem?> PinAsync(
        Guid userId,
        Guid taskId,
        bool pinned,
        CancellationToken cancellationToken = default);

    Task<TaskItem?> ArchiveAsync(
        Guid userId,
        Guid taskId,
        bool archived,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid userId,
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<TaskStatsSnapshot> GetStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<TaskSettingsSnapshot> GetSettingsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task UpdateSettingsAsync(
        Guid userId,
        TaskSettingsUpdate update,
        CancellationToken cancellationToken = default);

    Task<TaskDeveloperStats> GetDeveloperStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record TaskSettingsSnapshot(
    TaskSortOption DefaultSort,
    TaskPriority DefaultPriority,
    bool ShowCompleted);

public sealed record TaskSettingsUpdate(
    TaskSortOption? DefaultSort,
    TaskPriority? DefaultPriority,
    bool? ShowCompleted);

public sealed record TaskDeveloperStats(
    int TaskCount,
    int CompletedCount,
    long DatabaseSizeBytes);
