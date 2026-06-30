namespace Smartie.Application.Configuration;

public sealed class ChunkingOptions
{
    public const string SectionName = "Chunking";

    /// <summary>Target chunk size in characters (within the 1500–2500 range).</summary>
    public int TargetChunkSize { get; set; } = 2000;

    public int MinChunkSize { get; set; } = 1500;

    public int MaxChunkSize { get; set; } = 2500;

    /// <summary>Overlap between consecutive chunks in characters.</summary>
    public int ChunkOverlap { get; set; } = 250;
}
