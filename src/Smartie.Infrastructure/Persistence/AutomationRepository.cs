using Microsoft.EntityFrameworkCore;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Persistence;

public sealed class AutomationRepository : IAutomationRepository
{
    private readonly SmartieDbContext _db;

    public AutomationRepository(SmartieDbContext db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<AutomationRule>> ListAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Automations
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<AutomationRule>)t.Result, cancellationToken);

    public Task<AutomationRule?> FindAsync(Guid userId, Guid automationId, CancellationToken cancellationToken = default) =>
        _db.Automations
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Id == automationId, cancellationToken);

    public Task<AutomationRule?> FindForUpdateAsync(Guid userId, Guid automationId, CancellationToken cancellationToken = default) =>
        _db.Automations.FirstOrDefaultAsync(a => a.UserId == userId && a.Id == automationId, cancellationToken);

    public async Task<AutomationRule> AddAsync(AutomationRule rule, CancellationToken cancellationToken = default)
    {
        _db.Automations.Add(rule);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rule;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task<bool> DeleteAsync(Guid userId, Guid automationId, CancellationToken cancellationToken = default)
    {
        var rule = await _db.Automations
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Id == automationId, cancellationToken)
            .ConfigureAwait(false);
        if (rule is null)
        {
            return false;
        }

        _db.Automations.Remove(rule);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<IReadOnlyList<AutomationRule>> ListDueAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default) =>
        _db.Automations
            .Where(a => a.Enabled &&
                        a.TriggerType == AutomationTriggerType.Scheduled &&
                        a.NextRun != null &&
                        a.NextRun <= utcNow)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<AutomationRule>)t.Result, cancellationToken);

    public Task<IReadOnlyList<AutomationRule>> ListByTriggerAsync(
        Guid userId,
        AutomationTriggerType triggerType,
        bool enabledOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Automations.AsNoTracking().Where(a => a.UserId == userId && a.TriggerType == triggerType);
        if (enabledOnly)
        {
            query = query.Where(a => a.Enabled);
        }

        return query.ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<AutomationRule>)t.Result, cancellationToken);
    }

    public async Task<IReadOnlyList<AutomationRunLog>> ListRunLogsAsync(
        Guid userId,
        Guid? automationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AutomationRunLogs
            .AsNoTracking()
            .Include(l => l.AutomationRule)
            .Where(l => l.AutomationRule!.UserId == userId);

        if (automationId is not null)
        {
            query = query.Where(l => l.AutomationRuleId == automationId);
        }

        return await query
            .OrderByDescending(l => l.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddRunLogAsync(AutomationRunLog log, CancellationToken cancellationToken = default)
    {
        _db.AutomationRunLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AutomationStatsSnapshot> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rules = await _db.Automations
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var logs = await ListRunLogsAsync(userId, null, 10, cancellationToken).ConfigureAwait(false);
        var upcoming = rules
            .Where(a => a.Enabled && a.NextRun is not null)
            .OrderBy(a => a.NextRun)
            .Take(5)
            .Select(ToSnapshot)
            .ToList();

        return new AutomationStatsSnapshot(
            rules.Count,
            rules.Count(a => a.Enabled),
            rules.Count(a => !a.Enabled),
            upcoming,
            logs.Select(ToLogSnapshot).ToList());
    }

    public async Task<AutomationDeveloperStats> GetDeveloperStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rules = await _db.Automations
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var logs = await _db.AutomationRunLogs
            .AsNoTracking()
            .Include(l => l.AutomationRule)
            .Where(l => l.AutomationRule!.UserId == userId)
            .OrderByDescending(l => l.StartedAt)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var success = logs.Count(l => l.Status == AutomationRunStatus.Success);
        var failed = logs.Count(l => l.Status == AutomationRunStatus.Failed);
        var totalRuns = success + failed;
        var successRate = totalRuns == 0 ? 100d : success * 100d / totalRuns;

        return new AutomationDeveloperStats(
            rules.Count,
            rules.Count(a => a.Enabled),
            logs.Sum(l => l.DurationMs),
            failed,
            success,
            successRate,
            logs.Select(ToLogSnapshot).ToList());
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

    private static AutomationRunLogSnapshot ToLogSnapshot(AutomationRunLog log) =>
        new(
            log.Id,
            log.AutomationRuleId,
            log.AutomationRule?.Name ?? "Automation",
            log.Status,
            log.Message,
            log.DurationMs,
            log.StartedAt,
            log.CompletedAt);
}
