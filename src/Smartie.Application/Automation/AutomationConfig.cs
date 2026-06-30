using System.Text.Json;
using System.Text.Json.Serialization;
using Smartie.Domain.Entities;

namespace Smartie.Application.Automation;

public sealed class AutomationConfig
{
    public AutomationTriggerConfig Trigger { get; set; } = new();

    public AutomationConditionConfig Condition { get; set; } = new();

    public AutomationActionConfig Action { get; set; } = new();

    public static AutomationConfig Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AutomationConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<AutomationConfig>(json, JsonOptions) ?? new AutomationConfig();
        }
        catch (JsonException)
        {
            return new AutomationConfig();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}

public sealed class AutomationTriggerConfig
{
    public string Schedule { get; set; } = "Daily";

    public string Time { get; set; } = "08:00";

    public string? DayOfWeek { get; set; }
}

public sealed class AutomationConditionConfig
{
    public string Type { get; set; } = "None";

    public string? Value { get; set; }
}

public sealed class AutomationActionConfig
{
    public string? Prompt { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public Guid? DocumentId { get; set; }

    public Guid? ConversationId { get; set; }

    public string? SourcePath { get; set; }

    public string? DestinationPath { get; set; }

    public string? FilePath { get; set; }

    public bool SaveAsNote { get; set; }

    public bool RunFullIndex { get; set; }

    public string? TaskPriority { get; set; }
}

public sealed record AutomationEventContext(
    string? Keyword = null,
    string? DocumentType = null,
    string? TaskPriority = null,
    DateTimeOffset? EventDate = null,
    Guid? DocumentId = null,
    Guid? ConversationId = null,
    Guid? TaskId = null);

public static class AutomationScheduleHelper
{
    public static DateTimeOffset? ComputeNextRun(AutomationTriggerType triggerType, AutomationConfig config, DateTimeOffset utcNow)
    {
        if (triggerType != AutomationTriggerType.Scheduled)
        {
            return null;
        }

        if (!TimeOnly.TryParse(config.Trigger.Time, out var time))
        {
            time = new TimeOnly(8, 0);
        }

        var schedule = config.Trigger.Schedule.Trim();
        if (schedule.Equals("Weekly", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<DayOfWeek>(config.Trigger.DayOfWeek ?? "Sunday", true, out var day))
            {
                day = DayOfWeek.Sunday;
            }

            var candidate = utcNow.Date.Add(time.ToTimeSpan());
            var daysUntil = ((int)day - (int)utcNow.DayOfWeek + 7) % 7;
            if (daysUntil == 0 && candidate <= utcNow)
            {
                daysUntil = 7;
            }

            return new DateTimeOffset(utcNow.Date.AddDays(daysUntil).Add(time.ToTimeSpan()), TimeSpan.Zero);
        }

        var daily = utcNow.Date.Add(time.ToTimeSpan());
        if (daily <= utcNow)
        {
            daily = daily.AddDays(1);
        }

        return new DateTimeOffset(daily, TimeSpan.Zero);
    }
}

public static class AutomationConditionEvaluator
{
    public static bool Matches(AutomationConditionConfig condition, AutomationEventContext? context)
    {
        if (!Enum.TryParse<AutomationConditionType>(condition.Type, true, out var type) || type == AutomationConditionType.None)
        {
            return true;
        }

        if (context is null)
        {
            return type == AutomationConditionType.None;
        }

        return type switch
        {
            AutomationConditionType.ContainsKeyword =>
                !string.IsNullOrWhiteSpace(condition.Value) &&
                !string.IsNullOrWhiteSpace(context.Keyword) &&
                context.Keyword.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
            AutomationConditionType.DocumentType =>
                !string.IsNullOrWhiteSpace(condition.Value) &&
                !string.IsNullOrWhiteSpace(context.DocumentType) &&
                context.DocumentType.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
            AutomationConditionType.TaskPriority =>
                !string.IsNullOrWhiteSpace(condition.Value) &&
                !string.IsNullOrWhiteSpace(context.TaskPriority) &&
                context.TaskPriority.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
            AutomationConditionType.Date =>
                context.EventDate is { } eventDate &&
                DateOnly.TryParse(condition.Value, out var target) &&
                DateOnly.FromDateTime(eventDate.UtcDateTime) == target,
            _ => true
        };
    }
}
