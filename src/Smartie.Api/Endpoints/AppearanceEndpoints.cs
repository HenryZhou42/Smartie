using Smartie.Application.Abstractions;
using Smartie.Contracts;

namespace Smartie.Api.Endpoints;

public static class AppearanceEndpoints
{
    public static IEndpointRouteBuilder MapAppearanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/appearance");

        group.MapGet("/settings", async (IAppearanceService appearance, ICurrentUser user, CancellationToken ct) =>
        {
            var settings = await appearance.GetSettingsAsync(user.UserId, ct);
            return Results.Ok(ToDto(settings));
        });

        group.MapPut("/settings", async (
            UpdateAppearanceSettingsRequest request,
            IAppearanceService appearance,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var updated = await appearance.UpdateSettingsAsync(
                user.UserId,
                new AppearanceSettingsUpdate(
                    request.Theme,
                    request.AccentColor,
                    request.CustomAccentHex,
                    request.Density,
                    request.FontSize,
                    request.SidebarMode,
                    request.AnimationMode,
                    request.TransparencyEnabled,
                    request.WindowEffect,
                    request.TypingSpeedMs,
                    request.TransitionSpeedMs,
                    request.BubbleRadius,
                    request.BubbleWidth,
                    request.MessageSpacing,
                    request.CodeBlockTheme,
                    request.MarkdownTheme),
                ct);
            return Results.Ok(ToDto(updated));
        });

        group.MapGet("/options", (IAppearanceService appearance) =>
        {
            var options = appearance.GetOptions();
            return Results.Ok(new AppearanceOptionsDto(
                options.Themes,
                options.AccentColors,
                options.FontSizes,
                options.SidebarModes,
                options.Densities,
                options.AnimationModes,
                options.WindowEffects,
                options.BubbleRadii,
                options.BubbleWidths,
                options.MessageSpacings,
                options.CodeBlockThemes,
                options.MarkdownThemes));
        });

        group.MapGet("/developer", async (IAppearanceService appearance, ICurrentUser user, CancellationToken ct) =>
        {
            var snapshot = await appearance.GetDeveloperSnapshotAsync(user.UserId, ct);
            return Results.Ok(new AppearanceDeveloperDto(
                snapshot.Variables.Select(v => new ThemeVariableDto(v.Name, v.Value)).ToList()));
        });

        return app;
    }

    private static AppearanceSettingsDto ToDto(AppearanceSettingsSnapshot settings) =>
        new(
            settings.Theme,
            settings.AccentColor,
            settings.CustomAccentHex,
            settings.Density,
            settings.FontSize,
            settings.SidebarMode,
            settings.AnimationMode,
            settings.TransparencyEnabled,
            settings.WindowEffect,
            settings.TypingSpeedMs,
            settings.TransitionSpeedMs,
            settings.BubbleRadius,
            settings.BubbleWidth,
            settings.MessageSpacing,
            settings.CodeBlockTheme,
            settings.MarkdownTheme,
            settings.UpdatedAt);
}
