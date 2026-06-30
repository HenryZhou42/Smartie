namespace Smartie.Contracts;

public sealed record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    string Priority,
    DateTimeOffset? DueDate,
    bool Pinned,
    bool Archived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record CreateTaskRequest(
    string Title,
    string? Description = null,
    string? Priority = null,
    string? Status = null,
    DateTimeOffset? DueDate = null);

public sealed record UpdateTaskRequest(
    string Title,
    string? Description,
    string Priority,
    string Status,
    DateTimeOffset? DueDate,
    bool Pinned,
    bool Archived);

public sealed record PinTaskRequest(bool Pinned);

public sealed record ArchiveTaskRequest(bool Archived);

public sealed record TaskStatsDto(
    int TotalCount,
    int PendingCount,
    int CompletedCount,
    int DueTodayCount,
    int PinnedCount,
    int ArchivedCount,
    IReadOnlyList<TaskDto> RecentTasks);

public sealed record TaskSettingsDto(
    string DefaultSort,
    string DefaultPriority,
    bool ShowCompleted);

public sealed record UpdateTaskSettingsRequest(
    string? DefaultSort,
    string? DefaultPriority,
    bool? ShowCompleted);

public sealed record TaskDeveloperDto(
    int TaskCount,
    int CompletedCount,
    long DatabaseSizeBytes);
