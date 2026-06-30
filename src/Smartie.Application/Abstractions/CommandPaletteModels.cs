namespace Smartie.Application.Abstractions;

public sealed record PaletteCommandDefinition(
    string Id,
    string Title,
    string Subtitle,
    string Icon,
    string? Shortcut,
    IReadOnlyList<string> Keywords,
    string Route,
    bool Enabled = true);

public sealed record CommandSearchResult(
    string Id,
    string Title,
    string Subtitle,
    string Icon,
    string? Shortcut,
    string Route,
    bool Enabled,
    float RankingScore,
    int UsageCount,
    DateTimeOffset? LastUsed);

public sealed record CommandSearchResponse(
    IReadOnlyList<CommandSearchResult> Results,
    CommandPaletteDeveloperStats Developer);

public sealed record CommandPaletteDeveloperStats(
    int CommandCount,
    long SearchLatencyMs,
    float? TopRankingScore);
