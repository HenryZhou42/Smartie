using System.Diagnostics;
using Smartie.Application.Abstractions;
using Smartie.Application.Automation;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public sealed class AutomationService : IAutomationService, IAutomationEventPublisher
{
    private readonly IAutomationRepository _repository;
    private readonly AutomationActionExecutor _executor;

    public AutomationService(IAutomationRepository repository, AutomationActionExecutor executor)
    {
        _repository = repository;
        _executor = executor;
    }

    public async Task<IReadOnlyList<AutomationSnapshot>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rules = await _repository.ListAsync(userId, cancellationToken).ConfigureAwait(false);
        return rules.Select(ToSnapshot).ToList();
    }

    public async Task<AutomationSnapshot?> GetAsync(
        Guid userId,
        Guid automationId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _repository.FindAsync(userId, automationId, cancellationToken).ConfigureAwait(false);
        return rule is null ? null : ToSnapshot(rule);
    }

    public async Task<AutomationSnapshot> CreateAsync(
        Guid userId,
        CreateAutomationRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new ArgumentException("Automation name must not be empty.", nameof(request));
        }

        var trigger = ParseTriggerType(request.TriggerType);
        var action = ParseActionType(request.ActionType);
        var configJson = string.IsNullOrWhiteSpace(request.ConfigJson) ? new AutomationConfig().ToJson() : request.ConfigJson;
        var config = AutomationConfig.Parse(configJson);
        var now = DateTimeOffset.UtcNow;

        var rule = new AutomationRule
        {
            UserId = userId,
            Name = name,
            Description = request.Description?.Trim() ?? string.Empty,
            Enabled = request.Enabled ?? true,
            TriggerType = trigger,
            ActionType = action,
            ConfigJson = configJson,
            CreatedAt = now,
            UpdatedAt = now,
            NextRun = AutomationScheduleHelper.ComputeNextRun(trigger, config, now)
        };

        var created = await _repository.AddAsync(rule, cancellationToken).ConfigureAwait(false);
        return ToSnapshot(created);
    }

    public async Task<AutomationSnapshot?> UpdateAsync(
        Guid userId,
        Guid automationId,
        UpdateAutomationRequest request,
        CancellationToken cancellationToken = default)
    {
        var rule = await _repository.FindForUpdateAsync(userId, automationId, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return null;
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new ArgumentException("Automation name must not be empty.", nameof(request));
        }

        var trigger = ParseTriggerType(request.TriggerType);
        var action = ParseActionType(request.ActionType);
        var configJson = string.IsNullOrWhiteSpace(request.ConfigJson) ? rule.ConfigJson : request.ConfigJson;
        var config = AutomationConfig.Parse(configJson);
        var now = DateTimeOffset.UtcNow;

        rule.Name = name;
        rule.Description = request.Description?.Trim() ?? string.Empty;
        rule.Enabled = request.Enabled;
        rule.TriggerType = trigger;
        rule.ActionType = action;
        rule.ConfigJson = configJson;
        rule.UpdatedAt = now;
        rule.NextRun = rule.Enabled
            ? AutomationScheduleHelper.ComputeNextRun(trigger, config, now)
            : null;

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToSnapshot(rule);
    }

    public async Task<AutomationSnapshot?> EnableAsync(
        Guid userId,
        Guid automationId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _repository.FindForUpdateAsync(userId, automationId, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return null;
        }

        rule.Enabled = true;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        rule.NextRun = AutomationScheduleHelper.ComputeNextRun(
            rule.TriggerType,
            AutomationConfig.Parse(rule.ConfigJson),
            rule.UpdatedAt);

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToSnapshot(rule);
    }

    public async Task<AutomationSnapshot?> DisableAsync(
        Guid userId,
        Guid automationId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _repository.FindForUpdateAsync(userId, automationId, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return null;
        }

        rule.Enabled = false;
        rule.NextRun = null;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToSnapshot(rule);
    }

    public Task<bool> DeleteAsync(
        Guid userId,
        Guid automationId,
        CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(userId, automationId, cancellationToken);

    public async Task<AutomationRunResult> RunNowAsync(
        Guid userId,
        Guid automationId,
        AutomationEventContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var rule = await _repository.FindForUpdateAsync(userId, automationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Automation not found.");

        return await ExecuteRuleAsync(rule, context, cancellationToken).ConfigureAwait(false);
    }

    public async Task ProcessDueScheduledAsync(CancellationToken cancellationToken = default)
    {
        var due = await _repository.ListDueAsync(DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        foreach (var rule in due)
        {
            var tracked = await _repository.FindForUpdateAsync(rule.UserId, rule.Id, cancellationToken).ConfigureAwait(false);
            if (tracked is null || !tracked.Enabled)
            {
                continue;
            }

            await ExecuteRuleAsync(tracked, null, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task PublishAsync(
        Guid userId,
        AutomationTriggerType triggerType,
        AutomationEventContext context,
        CancellationToken cancellationToken = default) =>
        HandleEventAsync(userId, triggerType, context, cancellationToken);

    public async Task HandleEventAsync(
        Guid userId,
        AutomationTriggerType triggerType,
        AutomationEventContext context,
        CancellationToken cancellationToken = default)
    {
        var rules = await _repository.ListByTriggerAsync(userId, triggerType, enabledOnly: true, cancellationToken).ConfigureAwait(false);
        foreach (var snapshot in rules)
        {
            var rule = await _repository.FindForUpdateAsync(userId, snapshot.Id, cancellationToken).ConfigureAwait(false);
            if (rule is null)
            {
                continue;
            }

            var config = AutomationConfig.Parse(rule.ConfigJson);
            if (!AutomationConditionEvaluator.Matches(config.Condition, context))
            {
                continue;
            }

            await ExecuteRuleAsync(rule, context, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<AutomationRunLogSnapshot>> ListRunLogsAsync(
        Guid userId,
        Guid? automationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var logs = await _repository.ListRunLogsAsync(userId, automationId, limit, cancellationToken).ConfigureAwait(false);
        return logs.Select(log => new AutomationRunLogSnapshot(
            log.Id,
            log.AutomationRuleId,
            log.AutomationRule?.Name ?? "Automation",
            log.Status,
            log.Message,
            log.DurationMs,
            log.StartedAt,
            log.CompletedAt)).ToList();
    }

    public Task<AutomationStatsSnapshot> GetStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _repository.GetStatsAsync(userId, cancellationToken);

    public Task<AutomationDeveloperStats> GetDeveloperStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _repository.GetDeveloperStatsAsync(userId, cancellationToken);

    public async Task SeedExamplesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.ListAsync(userId, cancellationToken).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            return;
        }

        var dailySummary = new AutomationConfig
        {
            Trigger = new AutomationTriggerConfig { Schedule = "Daily", Time = "08:00" },
            Action = new AutomationActionConfig
            {
                Prompt = "Generate a concise morning overview of my priorities and recent activity.",
                Title = "Daily Summary",
                SaveAsNote = true
            }
        };

        var kbWatch = new AutomationConfig
        {
            Trigger = new AutomationTriggerConfig(),
            Action = new AutomationActionConfig { RunFullIndex = true }
        };

        var weeklyReview = new AutomationConfig
        {
            Trigger = new AutomationTriggerConfig { Schedule = "Weekly", Time = "09:00", DayOfWeek = "Sunday" },
            Action = new AutomationActionConfig
            {
                Prompt = "Collect completed tasks from this week and generate a short review report.",
                Title = "Weekly Review",
                SaveAsNote = true
            }
        };

        await CreateAsync(userId, new CreateAutomationRequest(
            "Daily Summary",
            "Every morning at 8am — Ask AI and save an overview note.",
            AutomationTriggerType.Scheduled.ToString(),
            AutomationActionType.AskAi.ToString(),
            dailySummary.ToJson(),
            false), cancellationToken).ConfigureAwait(false);

        await CreateAsync(userId, new CreateAutomationRequest(
            "Knowledge Base Watch",
            "When a document is uploaded — extract, chunk, and embed.",
            AutomationTriggerType.KnowledgeBaseUpdated.ToString(),
            AutomationActionType.SummarizeDocument.ToString(),
            kbWatch.ToJson(),
            false), cancellationToken).ConfigureAwait(false);

        await CreateAsync(userId, new CreateAutomationRequest(
            "Weekly Review",
            "Every Sunday — summarize completed tasks and save a report.",
            AutomationTriggerType.Scheduled.ToString(),
            AutomationActionType.RunPrompt.ToString(),
            weeklyReview.ToJson(),
            false), cancellationToken).ConfigureAwait(false);
    }

    private async Task<AutomationRunResult> ExecuteRuleAsync(
        AutomationRule rule,
        AutomationEventContext? context,
        CancellationToken cancellationToken)
    {
        var config = AutomationConfig.Parse(rule.ConfigJson);
        if (!AutomationConditionEvaluator.Matches(config.Condition, context))
        {
            var skipped = new AutomationRunLog
            {
                AutomationRuleId = rule.Id,
                Status = AutomationRunStatus.Skipped,
                Message = "Condition not met.",
                DurationMs = 0,
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow
            };
            await _repository.AddRunLogAsync(skipped, cancellationToken).ConfigureAwait(false);
            return new AutomationRunResult(rule.Id, AutomationRunStatus.Skipped, skipped.Message, 0);
        }

        var stopwatch = Stopwatch.StartNew();
        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            var message = await _executor.ExecuteAsync(rule.UserId, rule, config, context, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            rule.LastRun = startedAt;
            rule.RunCount++;
            rule.UpdatedAt = DateTimeOffset.UtcNow;
            rule.NextRun = rule.Enabled && rule.TriggerType == AutomationTriggerType.Scheduled
                ? AutomationScheduleHelper.ComputeNextRun(rule.TriggerType, config, rule.UpdatedAt)
                : rule.NextRun;

            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var log = new AutomationRunLog
            {
                AutomationRuleId = rule.Id,
                Status = AutomationRunStatus.Success,
                Message = message,
                DurationMs = stopwatch.ElapsedMilliseconds,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow
            };
            await _repository.AddRunLogAsync(log, cancellationToken).ConfigureAwait(false);
            return new AutomationRunResult(rule.Id, AutomationRunStatus.Success, message, log.DurationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            rule.LastRun = startedAt;
            rule.UpdatedAt = DateTimeOffset.UtcNow;
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var log = new AutomationRunLog
            {
                AutomationRuleId = rule.Id,
                Status = AutomationRunStatus.Failed,
                Message = ex.Message,
                DurationMs = stopwatch.ElapsedMilliseconds,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow
            };
            await _repository.AddRunLogAsync(log, cancellationToken).ConfigureAwait(false);
            return new AutomationRunResult(rule.Id, AutomationRunStatus.Failed, ex.Message, log.DurationMs);
        }
    }

    private static AutomationSnapshot ToSnapshot(AutomationRule rule) =>
        new(
            rule.Id,
            rule.Name,
            rule.Description,
            rule.Enabled,
            rule.TriggerType,
            rule.ActionType,
            rule.ConfigJson,
            rule.CreatedAt,
            rule.UpdatedAt,
            rule.LastRun,
            rule.NextRun,
            rule.RunCount);

    private static AutomationTriggerType ParseTriggerType(string value) =>
        Enum.TryParse<AutomationTriggerType>(value, true, out var parsed)
            ? parsed
            : AutomationTriggerType.Manual;

    private static AutomationActionType ParseActionType(string value) =>
        Enum.TryParse<AutomationActionType>(value, true, out var parsed)
            ? parsed
            : AutomationActionType.RunPrompt;
}
