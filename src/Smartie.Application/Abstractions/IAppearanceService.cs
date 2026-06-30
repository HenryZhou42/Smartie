using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IAppearanceService
{
    Task<AppearanceSettingsSnapshot> GetSettingsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AppearanceSettingsSnapshot> UpdateSettingsAsync(
        Guid userId,
        AppearanceSettingsUpdate update,
        CancellationToken cancellationToken = default);

    Task<AppearanceDeveloperSnapshot> GetDeveloperSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    AppearanceOptionsSnapshot GetOptions();
}

public sealed record AppearanceSettingsSnapshot(
    string Theme,
    string AccentColor,
    string? CustomAccentHex,
    string Density,
    string FontSize,
    string SidebarMode,
    string AnimationMode,
    bool TransparencyEnabled,
    string WindowEffect,
    int TypingSpeedMs,
    int TransitionSpeedMs,
    string BubbleRadius,
    string BubbleWidth,
    string MessageSpacing,
    string CodeBlockTheme,
    string MarkdownTheme,
    DateTimeOffset UpdatedAt);

public sealed record AppearanceSettingsUpdate(
    string? Theme,
    string? AccentColor,
    string? CustomAccentHex,
    string? Density,
    string? FontSize,
    string? SidebarMode,
    string? AnimationMode,
    bool? TransparencyEnabled,
    string? WindowEffect,
    int? TypingSpeedMs,
    int? TransitionSpeedMs,
    string? BubbleRadius,
    string? BubbleWidth,
    string? MessageSpacing,
    string? CodeBlockTheme,
    string? MarkdownTheme);

public sealed record AppearanceDeveloperSnapshot(IReadOnlyList<ThemeVariableEntry> Variables);

public sealed record ThemeVariableEntry(string Name, string Value);

public sealed record AppearanceOptionsSnapshot(
    IReadOnlyList<string> Themes,
    IReadOnlyList<string> AccentColors,
    IReadOnlyList<string> FontSizes,
    IReadOnlyList<string> SidebarModes,
    IReadOnlyList<string> Densities,
    IReadOnlyList<string> AnimationModes,
    IReadOnlyList<string> WindowEffects,
    IReadOnlyList<string> BubbleRadii,
    IReadOnlyList<string> BubbleWidths,
    IReadOnlyList<string> MessageSpacings,
    IReadOnlyList<string> CodeBlockThemes,
    IReadOnlyList<string> MarkdownThemes);

public static class AppearanceCatalog
{
    public static readonly string[] Themes = ["Default", "Dark", "Light", "Midnight", "OledBlack"];
    public static readonly string[] AccentColors = ["Purple", "Blue", "Green", "Orange", "Red", "Pink", "Custom"];
    public static readonly string[] FontSizes = ["Small", "Medium", "Large"];
    public static readonly string[] SidebarModes = ["Expanded", "Compact", "IconsOnly"];
    public static readonly string[] Densities = ["Default", "Compact", "Comfortable"];
    public static readonly string[] AnimationModes = ["Enabled", "Disabled", "ReducedMotion"];
    public static readonly string[] WindowEffects = ["Disabled", "Blur", "Transparency", "Mica", "Acrylic"];
    public static readonly string[] BubbleRadii = ["Small", "Medium", "Large"];
    public static readonly string[] BubbleWidths = ["Narrow", "Standard", "Wide"];
    public static readonly string[] MessageSpacings = ["Tight", "Normal", "Relaxed"];
    public static readonly string[] CodeBlockThemes = ["Default", "Monokai", "GitHub"];
    public static readonly string[] MarkdownThemes = ["Default", "GitHub", "Notion"];

    public static bool IsValid(string[] allowed, string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        allowed.Any(a => a.Equals(value, StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string[] allowed, string value, string fallback)
    {
        var match = allowed.FirstOrDefault(a => a.Equals(value, StringComparison.OrdinalIgnoreCase));
        return match ?? fallback;
    }
}
