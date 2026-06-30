namespace Smartie.Application.Configuration;

public sealed class MemoryOptions
{
    public const string SectionName = "Memory";

    public int DefaultMaxMemories { get; set; } = 200;

    public int DefaultRetentionDays { get; set; } = 365;

    public int DefaultSearchTopK { get; set; } = 5;

    public int MinSimilarityScorePercent { get; set; } = 45;

    public float MinSimilarityScore => MinSimilarityScorePercent / 100f;

    public float PinnedScoreBoost { get; set; } = 0.15f;
}
