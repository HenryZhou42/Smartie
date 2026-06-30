using Smartie.Application.Abstractions;
using Smartie.Contracts;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public sealed class AppearanceService : IAppearanceService
{
    private readonly IAppearanceRepository _repository;

    public AppearanceService(IAppearanceRepository repository)
    {
        _repository = repository;
    }

    public async Task<AppearanceSettingsSnapshot> GetSettingsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var preferences = await EnsurePreferencesAsync(userId, cancellationToken).ConfigureAwait(false);
        return ToSnapshot(preferences);
    }

    public async Task<AppearanceSettingsSnapshot> UpdateSettingsAsync(
        Guid userId,
        AppearanceSettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        var preferences = await EnsurePreferencesAsync(userId, cancellationToken).ConfigureAwait(false);

        if (update.Theme is { } theme)
        {
            preferences.Theme = AppearanceCatalog.Normalize(AppearanceCatalog.Themes, theme, "Default");
        }

        if (update.AccentColor is { } accent)
        {
            preferences.AccentColor = AppearanceCatalog.Normalize(AppearanceCatalog.AccentColors, accent, "Purple");
        }

        if (update.CustomAccentHex is not null)
        {
            preferences.CustomAccentHex = string.IsNullOrWhiteSpace(update.CustomAccentHex)
                ? null
                : NormalizeHex(update.CustomAccentHex);
        }

        if (update.Density is { } density)
        {
            preferences.Density = AppearanceCatalog.Normalize(AppearanceCatalog.Densities, density, "Default");
        }

        if (update.FontSize is { } fontSize)
        {
            preferences.FontSize = AppearanceCatalog.Normalize(AppearanceCatalog.FontSizes, fontSize, "Medium");
        }

        if (update.SidebarMode is { } sidebarMode)
        {
            preferences.SidebarMode = AppearanceCatalog.Normalize(AppearanceCatalog.SidebarModes, sidebarMode, "Expanded");
        }

        if (update.AnimationMode is { } animationMode)
        {
            preferences.AnimationMode = AppearanceCatalog.Normalize(AppearanceCatalog.AnimationModes, animationMode, "Enabled");
        }

        if (update.TransparencyEnabled is bool transparency)
        {
            preferences.TransparencyEnabled = transparency;
        }

        if (update.WindowEffect is { } windowEffect)
        {
            preferences.WindowEffect = AppearanceCatalog.Normalize(AppearanceCatalog.WindowEffects, windowEffect, "Disabled");
        }

        if (update.TypingSpeedMs is int typingSpeed && typingSpeed is >= 5 and <= 200)
        {
            preferences.TypingSpeedMs = typingSpeed;
        }

        if (update.TransitionSpeedMs is int transitionSpeed && transitionSpeed is >= 0 and <= 1000)
        {
            preferences.TransitionSpeedMs = transitionSpeed;
        }

        if (update.BubbleRadius is { } bubbleRadius)
        {
            preferences.BubbleRadius = AppearanceCatalog.Normalize(AppearanceCatalog.BubbleRadii, bubbleRadius, "Medium");
        }

        if (update.BubbleWidth is { } bubbleWidth)
        {
            preferences.BubbleWidth = AppearanceCatalog.Normalize(AppearanceCatalog.BubbleWidths, bubbleWidth, "Standard");
        }

        if (update.MessageSpacing is { } messageSpacing)
        {
            preferences.MessageSpacing = AppearanceCatalog.Normalize(AppearanceCatalog.MessageSpacings, messageSpacing, "Normal");
        }

        if (update.CodeBlockTheme is { } codeBlockTheme)
        {
            preferences.CodeBlockTheme = AppearanceCatalog.Normalize(AppearanceCatalog.CodeBlockThemes, codeBlockTheme, "Default");
        }

        if (update.MarkdownTheme is { } markdownTheme)
        {
            preferences.MarkdownTheme = AppearanceCatalog.Normalize(AppearanceCatalog.MarkdownThemes, markdownTheme, "Default");
        }

        preferences.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToSnapshot(preferences);
    }

    public async Task<AppearanceDeveloperSnapshot> GetDeveloperSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSettingsAsync(userId, cancellationToken).ConfigureAwait(false);
        var variables = AppearanceThemeMapper.Map(ToDto(snapshot));
        return new AppearanceDeveloperSnapshot(variables.Select(v => new ThemeVariableEntry(v.Name, v.Value)).ToList());
    }

    public AppearanceOptionsSnapshot GetOptions() =>
        new(
            AppearanceCatalog.Themes,
            AppearanceCatalog.AccentColors,
            AppearanceCatalog.FontSizes,
            AppearanceCatalog.SidebarModes,
            AppearanceCatalog.Densities,
            AppearanceCatalog.AnimationModes,
            AppearanceCatalog.WindowEffects,
            AppearanceCatalog.BubbleRadii,
            AppearanceCatalog.BubbleWidths,
            AppearanceCatalog.MessageSpacings,
            AppearanceCatalog.CodeBlockThemes,
            AppearanceCatalog.MarkdownThemes);

    private async Task<UserPreferences> EnsurePreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetForUpdateAsync(userId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var created = new UserPreferences { UserId = userId };
        return await _repository.AddAsync(created, cancellationToken).ConfigureAwait(false);
    }

    private static AppearanceSettingsSnapshot ToSnapshot(UserPreferences preferences) =>
        new(
            preferences.Theme,
            preferences.AccentColor,
            preferences.CustomAccentHex,
            preferences.Density,
            preferences.FontSize,
            preferences.SidebarMode,
            preferences.AnimationMode,
            preferences.TransparencyEnabled,
            preferences.WindowEffect,
            preferences.TypingSpeedMs,
            preferences.TransitionSpeedMs,
            preferences.BubbleRadius,
            preferences.BubbleWidth,
            preferences.MessageSpacing,
            preferences.CodeBlockTheme,
            preferences.MarkdownTheme,
            preferences.UpdatedAt);

    private static AppearanceSettingsDto ToDto(AppearanceSettingsSnapshot snapshot) =>
        new(
            snapshot.Theme,
            snapshot.AccentColor,
            snapshot.CustomAccentHex,
            snapshot.Density,
            snapshot.FontSize,
            snapshot.SidebarMode,
            snapshot.AnimationMode,
            snapshot.TransparencyEnabled,
            snapshot.WindowEffect,
            snapshot.TypingSpeedMs,
            snapshot.TransitionSpeedMs,
            snapshot.BubbleRadius,
            snapshot.BubbleWidth,
            snapshot.MessageSpacing,
            snapshot.CodeBlockTheme,
            snapshot.MarkdownTheme,
            snapshot.UpdatedAt);

    private static string? NormalizeHex(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith('#'))
        {
            trimmed = $"#{trimmed}";
        }

        return trimmed.Length is 4 or 7 or 9 ? trimmed : null;
    }
}
