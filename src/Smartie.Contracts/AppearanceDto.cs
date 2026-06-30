namespace Smartie.Contracts;

public sealed record AppearanceSettingsDto(
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

public sealed record UpdateAppearanceSettingsRequest(
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

public sealed record AppearanceDeveloperDto(
    IReadOnlyList<ThemeVariableDto> ActiveVariables);

public sealed record ThemeVariableDto(string Name, string Value);

public sealed record AppearanceOptionsDto(
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
