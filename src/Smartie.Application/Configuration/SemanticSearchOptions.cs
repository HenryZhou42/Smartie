namespace Smartie.Application.Configuration;

public sealed class SemanticSearchOptions
{
    public const string SectionName = "SemanticSearch";

    public int DefaultTopK { get; set; } = 5;

    public int MinSimilarityScorePercent { get; set; } = 50;

    public IReadOnlyList<int> AllowedTopKValues { get; set; } = [3, 5, 8, 10];

    public float MinSimilarityScore => MinSimilarityScorePercent / 100f;
}
