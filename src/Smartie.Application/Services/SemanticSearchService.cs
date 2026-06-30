using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;

namespace Smartie.Application.Services;

public sealed class SemanticSearchService : ISemanticSearchService
{
    private readonly IDocumentChunkRepository _chunks;
    private readonly IAiSettingsService _aiSettings;
    private readonly IEmbeddingProviderFactory _embeddingFactory;
    private readonly SemanticSearchOptions _options;
    private readonly ILogger<SemanticSearchService> _logger;
    private readonly IAppMetricsService? _metrics;

    public SemanticSearchService(
        IDocumentChunkRepository chunks,
        IAiSettingsService aiSettings,
        IEmbeddingProviderFactory embeddingFactory,
        IOptions<SemanticSearchOptions> options,
        ILogger<SemanticSearchService> logger,
        IAppMetricsService? metrics = null)
    {
        _chunks = chunks;
        _aiSettings = aiSettings;
        _embeddingFactory = embeddingFactory;
        _options = options.Value;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<SemanticSearchResultSet> SearchAsync(
        Guid userId,
        string query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(query))
        {
            return EmptyResult(topK, stopwatch.ElapsedMilliseconds);
        }

        var effectiveTopK = NormalizeTopK(topK);
        var searchable = await _chunks
            .GetSearchableChunksForUserAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        if (searchable.Count == 0)
        {
            return EmptyResult(effectiveTopK, stopwatch.ElapsedMilliseconds);
        }

        var settings = await _aiSettings.ResolveEmbeddingAsync(userId, cancellationToken).ConfigureAwait(false);
        var provider = _embeddingFactory.Create(settings);
        var queryVector = await provider
            .GenerateEmbeddingAsync(query.Trim(), cancellationToken)
            .ConfigureAwait(false);

        var scored = new List<SearchResult>(searchable.Count);
        foreach (var chunk in searchable)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkVector = EmbeddingVectorConverter.FromBytes(chunk.EmbeddingVector);
            var score = CosineSimilarity.CalculateCosineSimilarity(queryVector, chunkVector);
            if (score < _options.MinSimilarityScore)
            {
                continue;
            }

            scored.Add(new SearchResult(
                chunk.DocumentId,
                chunk.ChunkId,
                score,
                chunk.Content,
                chunk.FileName,
                chunk.PageNumber,
                chunk.ChunkIndex));
        }

        var results = scored
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.ChunkIndex)
            .Take(effectiveTopK)
            .ToList();

        stopwatch.Stop();
        _metrics?.RecordSearchLatency(stopwatch.ElapsedMilliseconds);

        _logger.LogInformation(
            "Semantic search for user {UserId} returned {ResultCount} result(s) from {ChunkCount} embedded chunks in {DurationMs}ms.",
            userId,
            results.Count,
            searchable.Count,
            stopwatch.ElapsedMilliseconds);

        return new SemanticSearchResultSet(
            results,
            new SemanticSearchDiagnostics(
                stopwatch.ElapsedMilliseconds,
                effectiveTopK,
                queryVector.Length,
                provider.ProviderName,
                results.Count > 0 ? results[0].Score : null));
    }

    private int NormalizeTopK(int topK)
    {
        if (_options.AllowedTopKValues.Contains(topK))
        {
            return topK;
        }

        return _options.DefaultTopK;
    }

    private static SemanticSearchResultSet EmptyResult(int topK, long durationMs) =>
        new(
            Array.Empty<SearchResult>(),
            new SemanticSearchDiagnostics(durationMs, topK, null, string.Empty, null));
}
