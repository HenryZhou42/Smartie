using Microsoft.EntityFrameworkCore;
using Smartie.Application.Abstractions;
using Smartie.Application.Services;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Persistence;

public sealed class TaskRepository : ITaskRepository
{
    private readonly SmartieDbContext _db;

    public TaskRepository(SmartieDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TaskItem>> ListAsync(
        Guid userId,
        TaskViewFilter view,
        string? search,
        TaskSortOption sort,
        bool showCompleted,
        CancellationToken cancellationToken = default)
    {
        var tasks = await _db.Tasks
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var utcNow = DateTimeOffset.UtcNow;
        var filtered = TaskQueryHelper.ApplyViewFilter(tasks, view, utcNow);
        filtered = TaskQueryHelper.ApplySearch(filtered, search);
        filtered = TaskQueryHelper.ApplyCompletedVisibility(filtered, showCompleted, view);
        return TaskQueryHelper.ApplySort(filtered, sort).ToList();
    }

    public Task<TaskItem?> FindAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default) =>
        _db.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Id == taskId, cancellationToken);

    public Task<TaskItem?> FindForUpdateAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default) =>
        _db.Tasks.FirstOrDefaultAsync(t => t.UserId == userId && t.Id == taskId, cancellationToken);

    public async Task<TaskItem> AddAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return task;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await _db.Tasks
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Id == taskId, cancellationToken)
            .ConfigureAwait(false);
        if (task is null)
        {
            return false;
        }

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<TaskStatsSnapshot> GetStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tasks = await _db.Tasks
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var utcNow = DateTimeOffset.UtcNow;
        var today = utcNow.Date;
        var active = tasks.Where(t => !t.Archived).ToList();

        var recent = active
            .OrderByDescending(t => t.Pinned)
            .ThenByDescending(t => t.UpdatedAt)
            .Take(5)
            .ToList();

        return new TaskStatsSnapshot(
            tasks.Count,
            active.Count(t => t.Status is SmartieTaskStatus.Pending or SmartieTaskStatus.InProgress),
            active.Count(t => t.Status == SmartieTaskStatus.Completed),
            active.Count(t =>
                t.DueDate is { } due &&
                due.UtcDateTime.Date == today &&
                t.Status is not SmartieTaskStatus.Completed and not SmartieTaskStatus.Cancelled),
            active.Count(t => t.Pinned),
            tasks.Count(t => t.Archived),
            recent);
    }

    public Task<User?> GetUserSettingsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
}
