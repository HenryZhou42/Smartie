using Smartie.Application.Automation;
using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IAutomationRepository
{
    Task<IReadOnlyList<AutomationRule>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AutomationRule?> FindAsync(Guid userId, Guid automationId, CancellationToken cancellationToken = default);

    Task<AutomationRule?> FindForUpdateAsync(Guid userId, Guid automationId, CancellationToken cancellationToken = default);

    Task<AutomationRule> AddAsync(AutomationRule rule, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid userId, Guid automationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationRule>> ListDueAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationRule>> ListByTriggerAsync(
        Guid userId,
        AutomationTriggerType triggerType,
        bool enabledOnly,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationRunLog>> ListRunLogsAsync(
        Guid userId,
        Guid? automationId,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddRunLogAsync(AutomationRunLog log, CancellationToken cancellationToken = default);

    Task<AutomationStatsSnapshot> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AutomationDeveloperStats> GetDeveloperStatsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IAutomationService
{
    Task<IReadOnlyList<AutomationSnapshot>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AutomationSnapshot?> GetAsync(Guid userId, Guid automationId, CancellationToken cancellationToken = default);

    Task<AutomationSnapshot> CreateAsync(
        Guid userId,
        CreateAutomationRequest request,
        CancellationToken cancellationToken = default);

    Task<AutomationSnapshot?> UpdateAsync(
        Guid userId,
        Guid automationId,
        UpdateAutomationRequest request,
        CancellationToken cancellationToken = default);

    Task<AutomationSnapshot?> EnableAsync(Guid userId, Guid automationId, CancellationToken cancellationToken = default);

    Task<AutomationSnapshot?> DisableAsync(Guid userId, Guid automationId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid userId, Guid automationId, CancellationToken cancellationToken = default);

    Task<AutomationRunResult> RunNowAsync(
        Guid userId,
        Guid automationId,
        AutomationEventContext? context = null,
        CancellationToken cancellationToken = default);

    Task ProcessDueScheduledAsync(CancellationToken cancellationToken = default);

    Task HandleEventAsync(
        Guid userId,
        AutomationTriggerType triggerType,
        AutomationEventContext context,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationRunLogSnapshot>> ListRunLogsAsync(
        Guid userId,
        Guid? automationId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<AutomationStatsSnapshot> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AutomationDeveloperStats> GetDeveloperStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SeedExamplesAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IAutomationEventPublisher
{
    Task PublishAsync(
        Guid userId,
        AutomationTriggerType triggerType,
        AutomationEventContext context,
        CancellationToken cancellationToken = default);
}

public sealed record CreateAutomationRequest(
    string Name,
    string? Description,
    string TriggerType,
    string ActionType,
    string? ConfigJson,
    bool? Enabled);

public sealed record UpdateAutomationRequest(
    string Name,
    string? Description,
    string TriggerType,
    string ActionType,
    string? ConfigJson,
    bool Enabled);

public sealed record AutomationSnapshot(
    Guid Id,
    string Name,
    string Description,
    bool Enabled,
    AutomationTriggerType TriggerType,
    AutomationActionType ActionType,
    string ConfigJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastRun,
    DateTimeOffset? NextRun,
    int RunCount);

public sealed record AutomationRunResult(
    Guid AutomationId,
    AutomationRunStatus Status,
    string Message,
    long DurationMs);

public sealed record AutomationRunLogSnapshot(
    Guid Id,
    Guid AutomationRuleId,
    string AutomationName,
    AutomationRunStatus Status,
    string Message,
    long DurationMs,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record AutomationStatsSnapshot(
    int TotalCount,
    int EnabledCount,
    int DisabledCount,
    IReadOnlyList<AutomationSnapshot> UpcomingRuns,
    IReadOnlyList<AutomationRunLogSnapshot> RecentRuns);

public sealed record AutomationDeveloperStats(
    int AutomationCount,
    int EnabledCount,
    long TotalExecutionTimeMs,
    int FailureCount,
    int SuccessCount,
    double SuccessRatePercent,
    IReadOnlyList<AutomationRunLogSnapshot> RecentLogs);
