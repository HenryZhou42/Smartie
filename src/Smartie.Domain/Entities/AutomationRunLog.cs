namespace Smartie.Domain.Entities;

public class AutomationRunLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AutomationRuleId { get; set; }

    public AutomationRule? AutomationRule { get; set; }

    public AutomationRunStatus Status { get; set; } = AutomationRunStatus.Running;

    public string Message { get; set; } = string.Empty;

    public long DurationMs { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }
}
