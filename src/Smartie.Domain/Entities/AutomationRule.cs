namespace Smartie.Domain.Entities;

/// <summary>
/// A local workflow rule: trigger, optional condition, and action stored in SQLite.
/// </summary>
public class AutomationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public AutomationTriggerType TriggerType { get; set; } = AutomationTriggerType.Manual;

    public AutomationActionType ActionType { get; set; } = AutomationActionType.RunPrompt;

    public string ConfigJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastRun { get; set; }

    public DateTimeOffset? NextRun { get; set; }

    public int RunCount { get; set; }

    public ICollection<AutomationRunLog> RunLogs { get; set; } = new List<AutomationRunLog>();
}
