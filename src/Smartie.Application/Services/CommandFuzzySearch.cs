using Smartie.Application.Abstractions;

namespace Smartie.Application.Services;

public static class CommandFuzzySearch
{
    public static int Score(string query, string target)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(target))
        {
            return 0;
        }

        var normalizedQuery = query.Trim().ToLowerInvariant();
        var normalizedTarget = target.Trim().ToLowerInvariant();

        if (normalizedTarget.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return 120 + normalizedQuery.Length * 8;
        }

        var queryIndex = 0;
        var score = 0;
        var consecutive = 0;

        for (var targetIndex = 0; targetIndex < normalizedTarget.Length && queryIndex < normalizedQuery.Length; targetIndex++)
        {
            if (normalizedTarget[targetIndex] != normalizedQuery[queryIndex])
            {
                consecutive = 0;
                continue;
            }

            consecutive++;
            score += 12 + consecutive * 6;

            if (targetIndex == 0 || !char.IsLetterOrDigit(normalizedTarget[targetIndex - 1]))
            {
                score += 8;
            }

            queryIndex++;
        }

        return queryIndex == normalizedQuery.Length ? score : 0;
    }

    public static int ScoreCommand(string query, PaletteCommandDefinition command)
    {
        var best = Score(query, command.Title);
        best = Math.Max(best, Score(query, command.Subtitle));

        foreach (var keyword in command.Keywords)
        {
            best = Math.Max(best, Score(query, keyword));
        }

        foreach (var part in command.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            best = Math.Max(best, Score(query, part));
        }

        return best;
    }
}
