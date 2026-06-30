namespace Smartie.Domain.Entities;

public enum AutomationTriggerType
{
    Manual,
    Scheduled,
    FileAdded,
    KnowledgeBaseUpdated,
    ConversationEnded,
    TaskCompleted
}
