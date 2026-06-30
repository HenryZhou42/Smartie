namespace Smartie.Contracts;

/// <summary>
/// Maps persisted appearance settings to CSS custom properties and data attributes.
/// </summary>
public static class AppearanceThemeMapper
{
    private static readonly Dictionary<string, string> AccentHex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Purple"] = "#7c5cff",
        ["Blue"] = "#3b82f6",
        ["Green"] = "#22c55e",
        ["Orange"] = "#f97316",
        ["Red"] = "#ef4444",
        ["Pink"] = "#ec4899"
    };

    public static string ResolveThemeAttribute(string theme) =>
        theme.ToLowerInvariant() switch
        {
            "dark" => "dark",
            "light" => "light",
            "midnight" => "midnight",
            "oledblack" => "oled",
            _ => "default"
        };

    public static string ResolveSidebarAttribute(string sidebarMode) =>
        sidebarMode.ToLowerInvariant() switch
        {
            "compact" => "compact",
            "iconsonly" => "icons-only",
            _ => "expanded"
        };

    public static string ResolveAccentHex(string accentColor, string? customAccentHex)
    {
        if (accentColor.Equals("Custom", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(customAccentHex))
        {
            return customAccentHex.Trim();
        }

        return AccentHex.TryGetValue(accentColor, out var hex) ? hex : AccentHex["Purple"];
    }

    public static IReadOnlyList<(string Name, string Value)> Map(AppearanceSettingsDto settings)
    {
        var accent = ResolveAccentHex(settings.AccentColor, settings.CustomAccentHex);
        var accentSoft = ToRgba(accent, 0.15);
        var accentGlow = ToRgba(accent, 0.35);
        var accentSecondary = ShiftHue(accent, -8);

        return
        [
            ("data-smartie-theme", ResolveThemeAttribute(settings.Theme)),
            ("data-smartie-sidebar", ResolveSidebarAttribute(settings.SidebarMode)),
            ("data-smartie-density", settings.Density.ToLowerInvariant()),
            ("data-smartie-font-size", settings.FontSize.ToLowerInvariant()),
            ("data-smartie-animations", settings.AnimationMode.ToLowerInvariant()),
            ("data-smartie-window-effect", settings.WindowEffect.ToLowerInvariant()),
            ("data-smartie-code-theme", settings.CodeBlockTheme.ToLowerInvariant()),
            ("data-smartie-markdown-theme", settings.MarkdownTheme.ToLowerInvariant()),
            ("--smartie-accent", accent),
            ("--smartie-accent-soft", accentSoft),
            ("--smartie-accent-glow", accentGlow),
            ("--smartie-user-bubble", $"linear-gradient(135deg, {accentSecondary}, {accent})"),
            ("--smartie-transition", $"{settings.TransitionSpeedMs}ms ease"),
            ("--smartie-typing-speed", $"{settings.TypingSpeedMs}ms"),
            ("--smartie-chat-bubble-radius", ResolveBubbleRadius(settings.BubbleRadius)),
            ("--smartie-chat-bubble-width", ResolveBubbleWidth(settings.BubbleWidth)),
            ("--smartie-chat-message-spacing", ResolveMessageSpacing(settings.MessageSpacing)),
            ("--smartie-panel-opacity", settings.TransparencyEnabled ? "0.92" : "1"),
            ("--smartie-shell-backdrop", ResolveWindowBackdrop(settings.WindowEffect, settings.TransparencyEnabled))
        ];
    }

    private static string ResolveBubbleRadius(string value) => value.ToLowerInvariant() switch
    {
        "small" => "12px",
        "large" => "22px",
        _ => "18px"
    };

    private static string ResolveBubbleWidth(string value) => value.ToLowerInvariant() switch
    {
        "narrow" => "min(520px, 78%)",
        "wide" => "min(920px, 96%)",
        _ => "min(720px, 88%)"
    };

    private static string ResolveMessageSpacing(string value) => value.ToLowerInvariant() switch
    {
        "tight" => "10px",
        "relaxed" => "22px",
        _ => "16px"
    };

    private static string ResolveWindowBackdrop(string windowEffect, bool transparencyEnabled)
    {
        if (!transparencyEnabled && windowEffect.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return "none";
        }

        return windowEffect.ToLowerInvariant() switch
        {
            "blur" => "blur(18px)",
            "transparency" => "none",
            "mica" => "blur(24px) saturate(1.2)",
            "acrylic" => "blur(28px) saturate(1.4)",
            _ => transparencyEnabled ? "blur(12px)" : "none"
        };
    }

    private static string ToRgba(string hex, double alpha)
    {
        var normalized = hex.TrimStart('#');
        if (normalized.Length == 3)
        {
            normalized = string.Concat(normalized.Select(c => $"{c}{c}"));
        }

        if (normalized.Length < 6 ||
            !int.TryParse(normalized[..6], System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return $"rgba(124, 92, 255, {alpha.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
        }

        var r = (rgb >> 16) & 0xFF;
        var g = (rgb >> 8) & 0xFF;
        var b = rgb & 0xFF;
        return $"rgba({r}, {g}, {b}, {alpha.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
    }

    private static string ShiftHue(string hex, int amount)
    {
        var normalized = hex.TrimStart('#');
        if (normalized.Length == 3)
        {
            normalized = string.Concat(normalized.Select(c => $"{c}{c}"));
        }

        if (normalized.Length < 6 ||
            !int.TryParse(normalized[..6], System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return hex;
        }

        var r = (rgb >> 16) & 0xFF;
        var g = (rgb >> 8) & 0xFF;
        var b = rgb & 0xFF;
        return $"#{Math.Clamp(r + amount, 0, 255):X2}{Math.Clamp(g + amount, 0, 255):X2}{Math.Clamp(b + amount, 0, 255):X2}";
    }
}
