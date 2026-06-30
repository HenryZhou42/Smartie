using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;

namespace Smartie.Application.Services;

public sealed class MemoryPromptBuilder : IMemoryPromptBuilder
{
    private readonly IMemoryService _memory;
    private readonly MemoryOptions _options;
    private readonly ILogger<MemoryPromptBuilder> _logger;

    public MemoryPromptBuilder(
        IMemoryService memory,
        IOptions<MemoryOptions> options,
        ILogger<MemoryPromptBuilder> logger)
    {
        _memory = memory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<(string? PromptBlock, MemoryRetrievalDiagnostics Diagnostics)> BuildMemoryContextAsync(
        Guid userId,
        string query,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var settings = await _memory.GetSettingsAsync(userId, cancellationToken).ConfigureAwait(false);

        if (!settings.Enabled)
        {
            stopwatch.Stop();
            return (null, new MemoryRetrievalDiagnostics(0, 0, null, stopwatch.ElapsedMilliseconds, settings.CurrentCount));
        }

        var pinned = await _memory
            .ListMemoriesAsync(userId, category: null, pinnedOnly: true, cancellationToken)
            .ConfigureAwait(false);
        var searched = await _memory
            .SearchMemoryAsync(userId, query, _options.DefaultSearchTopK, cancellationToken)
            .ConfigureAwait(false);

        var combined = pinned
            .Select(m => new MemorySearchResult(
                m.Id,
                m.Content,
                m.Category,
                m.Importance,
                Score: 1f,
                m.Pinned,
                m.CreatedAt,
                m.LastReferencedAt))
            .Concat(searched.Where(s => pinned.All(p => p.Id != s.MemoryId)))
            .GroupBy(r => r.MemoryId)
            .Select(g => g.First())
            .OrderByDescending(r => r.Pinned)
            .ThenByDescending(r => r.Score)
            .Take(_options.DefaultSearchTopK)
            .ToList();

        stopwatch.Stop();

        var diagnostics = new MemoryRetrievalDiagnostics(
            combined.Count,
            combined.Count,
            combined.Count > 0 ? combined[0].Score : null,
            stopwatch.ElapsedMilliseconds,
            settings.CurrentCount);

        if (combined.Count == 0)
        {
            _logger.LogDebug("No memories retrieved for user {UserId}.", userId);
            return (null, diagnostics);
        }

        var builder = new StringBuilder();
        builder.AppendLine("Known User Information");
        builder.AppendLine();
        foreach (var memory in combined)
        {
            builder.Append("- ");
            builder.AppendLine(memory.Content);
        }

        builder.AppendLine();
        builder.AppendLine("Instructions:");
        builder.AppendLine("- Use the known user information above when it helps answer the question.");
        builder.AppendLine("- Do not invent user facts that are not listed above.");
        builder.AppendLine("- If the answer is not supported by known user information, say you do not have that detail stored.");

        _logger.LogInformation(
            "Injected {MemoryCount} memories into prompt for user {UserId} in {DurationMs}ms.",
            combined.Count,
            userId,
            stopwatch.ElapsedMilliseconds);

        return (builder.ToString(), diagnostics);
    }
}
