namespace Smartie.Contracts;

public sealed record CommandDto(
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

public sealed record CommandSearchRequest(string? Query);

public sealed record CommandSearchResponseDto(
    IReadOnlyList<CommandDto> Results,
    CommandPaletteDeveloperDto Developer);

public sealed record CommandPaletteDeveloperDto(
    int CommandCount,
    long SearchLatencyMs,
    float? TopRankingScore);

public sealed record RecordCommandUsageRequest(string CommandId);
