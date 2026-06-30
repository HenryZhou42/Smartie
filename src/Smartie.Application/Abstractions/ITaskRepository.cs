using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface ITaskRepository
{
    Task<IReadOnlyList<TaskItem>> ListAsync(
        Guid userId,
        TaskViewFilter view,
        string? search,
        TaskSortOption sort,
        bool showCompleted,
        CancellationToken cancellationToken = default);

    Task<TaskItem?> FindAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default);

    Task<TaskItem?> FindForUpdateAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default);

    Task<TaskItem> AddAsync(TaskItem task, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default);

    Task<TaskStatsSnapshot> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<User?> GetUserSettingsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record TaskStatsSnapshot(
    int TotalCount,
    int PendingCount,
    int CompletedCount,
    int DueTodayCount,
    int PinnedCount,
    int ArchivedCount,
    IReadOnlyList<TaskItem> RecentTasks);
