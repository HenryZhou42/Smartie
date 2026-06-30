namespace Smartie.Domain.Entities;

/// <summary>
/// An application user. Community Edition uses a single local profile with no
/// login; the schema is shaped so cloud accounts can be added later without
/// migration churn.
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = "Local User";

    public string? Email { get; set; }

    /// <summary>Lowercase key of the AI provider this user has selected (see AiProviderCatalog).</summary>
    public string SelectedAiProvider { get; set; } = "google";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool MemoryEnabled { get; set; } = true;

    public int MaxMemories { get; set; } = 200;

    public int MemoryRetentionDays { get; set; } = 365;

    public TaskSortOption TaskDefaultSort { get; set; } = TaskSortOption.DueDate;

    public TaskPriority TaskDefaultPriority { get; set; } = TaskPriority.Medium;

    public bool TaskShowCompleted { get; set; } = true;

    public int FileMaxRecentFiles { get; set; } = 50;

    public bool FileShowHiddenFiles { get; set; }

    public bool HasCompletedOnboarding { get; set; }

    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public ICollection<AiProviderCredential> AiProviderCredentials { get; set; } = new List<AiProviderCredential>();

    public ICollection<Document> Documents { get; set; } = new List<Document>();

    public UserPreferences? Preferences { get; set; }
}
