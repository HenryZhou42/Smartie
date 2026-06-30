using System.Text.RegularExpressions;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public sealed class MemoryExtractor : IMemoryExtractor
{
    private static readonly (Regex Pattern, MemoryCategory Category, MemoryImportance Importance, Func<Match, string> Format)[] Rules =
    [
        (new(@"^\s*remember\s+(?:that\s+)?(?<value>.+?)\.?\s*$", RegexOptions.IgnoreCase), MemoryCategory.Custom, MemoryImportance.High, m => Normalize(m.Groups["value"].Value)),
        (new(@"\b(?:i|we)\s+prefer(?:s)?\s+(?<value>.+?)(?:[\.!]|$)", RegexOptions.IgnoreCase), MemoryCategory.Preferences, MemoryImportance.High, m => $"Prefers {Normalize(m.Groups["value"].Value)}"),
        (new(@"\b(?:i|we)\s+(?:like|love)\s+(?<value>.+?)(?:[\.!]|$)", RegexOptions.IgnoreCase), MemoryCategory.Preferences, MemoryImportance.Medium, m => $"Likes {Normalize(m.Groups["value"].Value)}"),
        (new(@"\b(?:i'?m|i am)\s+(?:working on|building|creating|developing)\s+(?<value>.+?)(?:[\.!]|$)", RegexOptions.IgnoreCase), MemoryCategory.Projects, MemoryImportance.High, m => $"Building {Normalize(m.Groups["value"].Value)}"),
        (new(@"\b(?:i|we)\s+use(?:s)?\s+(?<value>.+?)(?:[\.!]|$)", RegexOptions.IgnoreCase), MemoryCategory.Technical, MemoryImportance.Medium, m => $"Uses {Normalize(m.Groups["value"].Value)}"),
        (new(@"\b(?:i'?m|i am)\s+interested in\s+(?<value>.+?)(?:[\.!]|$)", RegexOptions.IgnoreCase), MemoryCategory.Interests, MemoryImportance.Medium, m => $"Interested in {Normalize(m.Groups["value"].Value)}"),
        (new(@"\b(?:i|we)\s+want\s+(?<value>.+?)(?:[\.!]|$)", RegexOptions.IgnoreCase), MemoryCategory.Goals, MemoryImportance.Medium, m => $"Wants {Normalize(m.Groups["value"].Value)}"),
        (new(@"\b(?:i|we)\s+work(?:s)?\s+in\s+(?<value>.+?)(?:[\.!]|$)", RegexOptions.IgnoreCase), MemoryCategory.Work, MemoryImportance.Medium, m => $"Works in {Normalize(m.Groups["value"].Value)}"),
        (new(@"\bmy\s+name\s+is\s+(?<value>[A-Za-z][A-Za-z\s'\-]{1,40})", RegexOptions.IgnoreCase), MemoryCategory.People, MemoryImportance.High, m => $"Name is {Normalize(m.Groups["value"].Value)}"),
    ];

    public IReadOnlyList<ExtractedMemoryCandidate> Extract(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return Array.Empty<ExtractedMemoryCandidate>();
        }

        var results = new List<ExtractedMemoryCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (pattern, category, importance, format) in Rules)
        {
            var match = pattern.Match(userMessage);
            if (!match.Success)
            {
                continue;
            }

            var content = format(match).Trim();
            if (content.Length < 3 || !seen.Add(content))
            {
                continue;
            }

            results.Add(new ExtractedMemoryCandidate(content, category, importance));
        }

        return results;
    }

    private static string Normalize(string value) =>
        Regex.Replace(value.Trim().TrimEnd('.'), @"\s+", " ");
}
