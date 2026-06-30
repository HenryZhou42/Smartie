namespace Smartie.Contracts;

public sealed record AutomationDto(
    Guid Id,
    string Name,
    string Description,
    bool Enabled,
    string TriggerType,
    string ActionType,
    string ConfigJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastRun,
    DateTimeOffset? NextRun,
    int RunCount);

public sealed record CreateAutomationRequest(
    string Name,
    string? Description = null,
    string TriggerType = "Manual",
    string ActionType = "RunPrompt",
    string? ConfigJson = null,
    bool? Enabled = null);

public sealed record UpdateAutomationRequest(
    string Name,
    string? Description,
    string TriggerType,
    string ActionType,
    string? ConfigJson,
    bool Enabled);

public sealed record AutomationRunResultDto(
    Guid AutomationId,
    string Status,
    string Message,
    long DurationMs);

public sealed record AutomationRunLogDto(
    Guid Id,
    Guid AutomationRuleId,
    string AutomationName,
    string Status,
    string Message,
    long DurationMs,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record AutomationStatsDto(
    int TotalCount,
    int EnabledCount,
    int DisabledCount,
    IReadOnlyList<AutomationDto> UpcomingRuns,
    IReadOnlyList<AutomationRunLogDto> RecentRuns);

public sealed record AutomationDeveloperDto(
    int AutomationCount,
    int EnabledCount,
    long TotalExecutionTimeMs,
    int FailureCount,
    int SuccessCount,
    double SuccessRatePercent,
    IReadOnlyList<AutomationRunLogDto> RecentLogs);

public sealed record AutomationOptionsDto(
    IReadOnlyList<string> TriggerTypes,
    IReadOnlyList<string> ActionTypes,
    IReadOnlyList<string> ConditionTypes,
    IReadOnlyList<string> ScheduleFrequencies);
