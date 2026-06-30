namespace Smartie.Domain.Entities;

/// <summary>
/// Appearance and personalization preferences for a user profile.
/// </summary>
public class UserPreferences
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string Theme { get; set; } = "Default";

    public string AccentColor { get; set; } = "Purple";

    public string? CustomAccentHex { get; set; }

    public string Density { get; set; } = "Default";

    public string FontSize { get; set; } = "Medium";

    public string SidebarMode { get; set; } = "Expanded";

    public string AnimationMode { get; set; } = "Enabled";

    public bool TransparencyEnabled { get; set; }

    public string WindowEffect { get; set; } = "Disabled";

    public int TypingSpeedMs { get; set; } = 20;

    public int TransitionSpeedMs { get; set; } = 200;

    public string BubbleRadius { get; set; } = "Medium";

    public string BubbleWidth { get; set; } = "Standard";

    public string MessageSpacing { get; set; } = "Normal";

    public string CodeBlockTheme { get; set; } = "Default";

    public string MarkdownTheme { get; set; } = "Default";

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
