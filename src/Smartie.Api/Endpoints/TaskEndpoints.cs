using Smartie.Application.Abstractions;
using Smartie.Contracts;
using Smartie.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Smartie.Infrastructure.Persistence;

namespace Smartie.Api.Endpoints;

public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks");

        group.MapGet("/", async (
            string? view,
            string? search,
            string? sort,
            ITaskService tasks,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var parsedView = Enum.TryParse<TaskViewFilter>(view, true, out var viewValue)
                ? viewValue
                : TaskViewFilter.All;
            TaskSortOption? parsedSort = Enum.TryParse<TaskSortOption>(sort, true, out var sortValue)
                ? sortValue
                : null;

            var results = await tasks.ListAsync(user.UserId, parsedView, search, parsedSort, ct);
            return Results.Ok(results.Select(ToDto));
        });

        group.MapGet("/stats", async (ITaskService tasks, ICurrentUser user, CancellationToken ct) =>
        {
            var stats = await tasks.GetStatsAsync(user.UserId, ct);
            return Results.Ok(new TaskStatsDto(
                stats.TotalCount,
                stats.PendingCount,
                stats.CompletedCount,
                stats.DueTodayCount,
                stats.PinnedCount,
                stats.ArchivedCount,
                stats.RecentTasks.Select(ToDto).ToList()));
        });

        group.MapGet("/settings", async (ITaskService tasks, ICurrentUser user, CancellationToken ct) =>
        {
            var settings = await tasks.GetSettingsAsync(user.UserId, ct);
            return Results.Ok(new TaskSettingsDto(
                settings.DefaultSort.ToString(),
                settings.DefaultPriority.ToString(),
                settings.ShowCompleted));
        });

        group.MapPut("/settings", async (
            UpdateTaskSettingsRequest request,
            ITaskService tasks,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            TaskSortOption? sort = Enum.TryParse<TaskSortOption>(request.DefaultSort, true, out var parsedSort)
                ? parsedSort
                : null;
            TaskPriority? priority = Enum.TryParse<TaskPriority>(request.DefaultPriority, true, out var parsedPriority)
                ? parsedPriority
                : null;

            await tasks.UpdateSettingsAsync(
                user.UserId,
                new TaskSettingsUpdate(sort, priority, request.ShowCompleted),
                ct);

            var settings = await tasks.GetSettingsAsync(user.UserId, ct);
            return Results.Ok(new TaskSettingsDto(
                settings.DefaultSort.ToString(),
                settings.DefaultPriority.ToString(),
                settings.ShowCompleted));
        });

        group.MapGet("/developer", async (
            ITaskService tasks,
            SmartieDbContext db,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var stats = await tasks.GetDeveloperStatsAsync(user.UserId, ct);
            var databaseSize = GetDatabaseSizeBytes(db);
            return Results.Ok(new TaskDeveloperDto(
                stats.TaskCount,
                stats.CompletedCount,
                databaseSize));
        });

        group.MapPost("/", async (
            CreateTaskRequest request,
            ITaskService tasks,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest("Title must not be empty.");
            }

            TaskPriority? priority = Enum.TryParse<TaskPriority>(request.Priority, true, out var parsedPriority)
                ? parsedPriority
                : null;
            SmartieTaskStatus? status = Enum.TryParse<SmartieTaskStatus>(request.Status, true, out var parsedStatus)
                ? parsedStatus
                : null;

            var created = await tasks.CreateAsync(
                user.UserId,
                request.Title,
                request.Description,
                priority,
                status,
                request.DueDate,
                ct);
            return Results.Ok(ToDto(created));
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateTaskRequest request,
            ITaskService tasks,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest("Title must not be empty.");
            }

            if (!TryParsePriority(request.Priority, out var priority))
            {
                return Results.BadRequest($"Unknown priority '{request.Priority}'.");
            }

            if (!TryParseStatus(request.Status, out var status))
            {
                return Results.BadRequest($"Unknown status '{request.Status}'.");
            }

            var updated = await tasks.UpdateAsync(
                user.UserId,
                id,
                request.Title,
                request.Description,
                priority,
                status,
                request.DueDate,
                request.Pinned,
                request.Archived,
                ct);
            return updated is null ? Results.NotFound() : Results.Ok(ToDto(updated));
        });

        group.MapPut("/{id:guid}/complete", async (
            Guid id,
            ITaskService tasks,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var updated = await tasks.CompleteAsync(user.UserId, id, ct);
            return updated is null ? Results.NotFound() : Results.Ok(ToDto(updated));
        });

        group.MapPut("/{id:guid}/pin", async (
            Guid id,
            PinTaskRequest request,
            ITaskService tasks,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var updated = await tasks.PinAsync(user.UserId, id, request.Pinned, ct);
            return updated is null ? Results.NotFound() : Results.Ok(ToDto(updated));
        });

        group.MapPut("/{id:guid}/archive", async (
            Guid id,
            ArchiveTaskRequest request,
            ITaskService tasks,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var updated = await tasks.ArchiveAsync(user.UserId, id, request.Archived, ct);
            return updated is null ? Results.NotFound() : Results.Ok(ToDto(updated));
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ITaskService tasks,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var deleted = await tasks.DeleteAsync(user.UserId, id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }

    private static TaskDto ToDto(TaskItem task) =>
        new(
            task.Id,
            task.Title,
            task.Description,
            task.Status.ToString(),
            task.Priority.ToString(),
            task.DueDate,
            task.Pinned,
            task.Archived,
            task.CreatedAt,
            task.UpdatedAt,
            task.CompletedAt);

    private static bool TryParseStatus(string value, out SmartieTaskStatus status) =>
        Enum.TryParse(value, ignoreCase: true, out status);

    private static bool TryParsePriority(string value, out TaskPriority priority) =>
        Enum.TryParse(value, ignoreCase: true, out priority);

    private static long GetDatabaseSizeBytes(SmartieDbContext db)
    {
        try
        {
            var connectionString = db.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return 0;
            }

            const string prefix = "Data Source=";
            var index = connectionString.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return 0;
            }

            var path = connectionString[(index + prefix.Length)..].Trim().Trim('"');
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }
}
