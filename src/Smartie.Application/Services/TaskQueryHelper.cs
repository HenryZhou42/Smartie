using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public static class TaskQueryHelper
{
    public static IEnumerable<TaskItem> ApplyViewFilter(IEnumerable<TaskItem> tasks, TaskViewFilter view, DateTimeOffset utcNow)
    {
        var today = utcNow.Date;

        return view switch
        {
            TaskViewFilter.All => tasks.Where(t => !t.Archived),
            TaskViewFilter.Today => tasks.Where(t =>
                !t.Archived &&
                t.DueDate is { } due &&
                due.UtcDateTime.Date == today),
            TaskViewFilter.Upcoming => tasks.Where(t =>
                !t.Archived &&
                t.DueDate is { } due &&
                due.UtcDateTime.Date > today),
            TaskViewFilter.Completed => tasks.Where(t =>
                !t.Archived &&
                t.Status == SmartieTaskStatus.Completed),
            TaskViewFilter.Pinned => tasks.Where(t => !t.Archived && t.Pinned),
            TaskViewFilter.Archived => tasks.Where(t => t.Archived),
            _ => tasks
        };
    }

    public static IEnumerable<TaskItem> ApplySearch(IEnumerable<TaskItem> tasks, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return tasks;
        }

        return tasks.Where(t =>
            t.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            (t.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    public static IEnumerable<TaskItem> ApplyCompletedVisibility(IEnumerable<TaskItem> tasks, bool showCompleted, TaskViewFilter view)
    {
        if (showCompleted || view is TaskViewFilter.Completed or TaskViewFilter.Archived)
        {
            return tasks;
        }

        return tasks.Where(t => t.Status is not SmartieTaskStatus.Completed and not SmartieTaskStatus.Cancelled);
    }

    public static IOrderedEnumerable<TaskItem> ApplySort(IEnumerable<TaskItem> tasks, TaskSortOption sort) =>
        sort switch
        {
            TaskSortOption.Priority => tasks
                .OrderByDescending(t => t.Pinned)
                .ThenByDescending(t => t.Priority)
                .ThenBy(t => t.DueDate ?? DateTimeOffset.MaxValue)
                .ThenByDescending(t => t.UpdatedAt),
            TaskSortOption.CreatedAt => tasks
                .OrderByDescending(t => t.Pinned)
                .ThenByDescending(t => t.CreatedAt),
            TaskSortOption.UpdatedAt => tasks
                .OrderByDescending(t => t.Pinned)
                .ThenByDescending(t => t.UpdatedAt),
            TaskSortOption.Title => tasks
                .OrderByDescending(t => t.Pinned)
                .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase),
            _ => tasks
                .OrderByDescending(t => t.Pinned)
                .ThenBy(t => t.DueDate ?? DateTimeOffset.MaxValue)
                .ThenByDescending(t => t.Priority)
                .ThenByDescending(t => t.UpdatedAt)
        };
}
