using System.Diagnostics;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public sealed class CommandPaletteService : ICommandPaletteService
{
    private const int MaxDynamicConversations = 8;
    private const int MaxResults = 20;

    private readonly IRecentCommandRepository _recentCommands;
    private readonly IConversationRepository _conversations;
    private readonly IPluginRegistry _plugins;

    public CommandPaletteService(
        IRecentCommandRepository recentCommands,
        IConversationRepository conversations,
        IPluginRegistry plugins)
    {
        _recentCommands = recentCommands;
        _conversations = conversations;
        _plugins = plugins;
    }

    public async Task<CommandSearchResponse> SearchAsync(
        Guid userId,
        string? query,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var trimmedQuery = query?.Trim() ?? string.Empty;
        var hasQuery = trimmedQuery.Length > 0;

        var usage = await _recentCommands
            .ListForUserAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        var usageByName = usage.ToDictionary(u => u.CommandName, StringComparer.OrdinalIgnoreCase);

        var candidates = BuildCandidates(userId, usageByName, cancellationToken);
        var conversationRows = await _conversations
            .ListAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var conversation in conversationRows.Take(MaxDynamicConversations))
        {
            var commandId = $"conversation.open:{conversation.Id:N}";
            usageByName.TryGetValue(commandId, out var usageRow);
            candidates.Add(new ScoredPaletteCommand(
                commandId,
                conversation.Title,
                "Recent Conversations",
                "chat",
                null,
                $"/chat?c={conversation.Id}",
                true,
                usageRow?.UsageCount ?? 0,
                usageRow?.LastUsed,
                FuzzyScore: 0));
        }

        IEnumerable<ScoredPaletteCommand> filtered = candidates;
        if (hasQuery)
        {
            filtered = candidates
                .Select(command =>
                {
                    var fuzzy = CommandFuzzySearch.ScoreCommand(trimmedQuery, new PaletteCommandDefinition(
                        command.Id,
                        command.Title,
                        command.Subtitle,
                        command.Icon,
                        command.Shortcut,
                        BuildKeywords(command),
                        command.Route,
                        command.Enabled));

                    return command with { FuzzyScore = fuzzy };
                })
                .Where(command => command.FuzzyScore > 0)
                .OrderByDescending(command => command.FuzzyScore)
                .ThenByDescending(command => ComputeRankingScore(command, hasQuery: true));
        }
        else
        {
            filtered = candidates
                .OrderByDescending(command => command.LastUsed ?? DateTimeOffset.MinValue)
                .ThenByDescending(command => command.UsageCount)
                .ThenBy(command => command.Title, StringComparer.OrdinalIgnoreCase);
        }

        var results = filtered
            .Take(MaxResults)
            .Select(command => new CommandSearchResult(
                command.Id,
                command.Title,
                command.Subtitle,
                command.Icon,
                command.Shortcut,
                command.Route,
                command.Enabled,
                ComputeRankingScore(command, hasQuery),
                command.UsageCount,
                command.LastUsed))
            .ToList();

        stopwatch.Stop();

        return new CommandSearchResponse(
            results,
            new CommandPaletteDeveloperStats(
                results.Count,
                stopwatch.ElapsedMilliseconds,
                results.Count > 0 ? results[0].RankingScore : null));
    }

    public Task RecordUsageAsync(
        Guid userId,
        string commandId,
        CancellationToken cancellationToken = default) =>
        _recentCommands.RecordUsageAsync(userId, commandId, cancellationToken);

    public async Task<CommandPaletteDeveloperStats> GetDeveloperStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = await SearchAsync(userId, query: null, cancellationToken).ConfigureAwait(false);
        return response.Developer with { CommandCount = response.Results.Count };
    }

    private List<ScoredPaletteCommand> BuildCandidates(
        Guid userId,
        IReadOnlyDictionary<string, RecentCommand> usageByName,
        CancellationToken cancellationToken)
    {
        _ = userId;
        _ = cancellationToken;

        return CommandCatalog.GetStaticCommands()
            .Select(command =>
            {
                usageByName.TryGetValue(command.Id, out var usageRow);
                return new ScoredPaletteCommand(
                    command.Id,
                    command.Title,
                    command.Subtitle,
                    command.Icon,
                    command.Shortcut,
                    command.Route,
                    command.Enabled,
                    usageRow?.UsageCount ?? 0,
                    usageRow?.LastUsed,
                    FuzzyScore: 0);
            })
            .Concat(_plugins.GetCommands(enabledOnly: true).Select(command =>
            {
                var commandId = $"plugin.{command.PluginKey}.{command.Id}";
                usageByName.TryGetValue(commandId, out var usageRow);
                return new ScoredPaletteCommand(
                    commandId,
                    command.Title,
                    $"{command.PluginName} · Plugin",
                    command.Icon,
                    null,
                    command.Route,
                    command.PluginEnabled,
                    usageRow?.UsageCount ?? 0,
                    usageRow?.LastUsed,
                    FuzzyScore: 0);
            }))
            .ToList();
    }

    private static float ComputeRankingScore(ScoredPaletteCommand command, bool hasQuery)
    {
        var score = hasQuery ? command.FuzzyScore : 0f;
        score += command.UsageCount * 4f;

        if (command.LastUsed is { } lastUsed)
        {
            var ageDays = Math.Max(0, (DateTimeOffset.UtcNow - lastUsed).TotalDays);
            score += Math.Max(0f, 30f - (float)ageDays);
        }

        if (!command.Enabled)
        {
            score *= 0.35f;
        }

        return score;
    }

    private static IReadOnlyList<string> BuildKeywords(ScoredPaletteCommand command) =>
        command.Id.StartsWith("plugin.", StringComparison.OrdinalIgnoreCase)
            ? [command.Title, command.Subtitle, "plugin"]
            : [command.Title, command.Subtitle];

    private sealed record ScoredPaletteCommand(
        string Id,
        string Title,
        string Subtitle,
        string Icon,
        string? Shortcut,
        string Route,
        bool Enabled,
        int UsageCount,
        DateTimeOffset? LastUsed,
        int FuzzyScore);
}
