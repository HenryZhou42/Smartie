using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Application.Services;
using Smartie.Contracts;
using Smartie.Domain.Entities;

namespace Smartie.Api.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents");

        group.MapGet("/", async (
            string? search,
            IDocumentService service,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var documents = await service.ListAsync(user.UserId, search, ct);
            return Results.Ok(documents.Select(ToDto));
        });

        group.MapGet("/stats", async (IDocumentService service, ICurrentUser user, CancellationToken ct) =>
        {
            var stats = await service.GetStatsAsync(user.UserId, ct);
            return Results.Ok(new DocumentStatsDto(
                stats.DocumentCount,
                CollectionCount: 0,
                stats.IndexedCount,
                stats.TotalSizeBytes,
                stats.RecentUploadCount,
                stats.ExtractedCount,
                stats.TotalExtractedCharacters,
                stats.LastExtractedAt,
                stats.LastExtractorUsed,
                stats.ChunkedCount,
                stats.TotalChunkCount,
                stats.EmbeddedDocumentCount,
                stats.TotalEmbeddedChunkCount));
        });

        group.MapGet("/extraction/developer", async (IDocumentService service, ICurrentUser user, CancellationToken ct) =>
        {
            var stats = await service.GetStatsAsync(user.UserId, ct);
            var documents = await service.ListAsync(user.UserId, search: null, ct);
            var last = documents
                .Where(d => d.ExtractionStatus == DocumentExtractionStatus.Completed)
                .OrderByDescending(d => d.ExtractedAt ?? d.UpdatedAt)
                .FirstOrDefault();

            return Results.Ok(new DocumentExtractionDeveloperDto(
                stats.ExtractedCount,
                stats.TotalExtractedCharacters,
                stats.LastExtractedAt,
                stats.LastExtractorUsed,
                last?.ExtractionDurationMs));
        });

        group.MapGet("/chunking/developer", async (
            IDocumentService service,
            IDocumentChunkRepository chunks,
            IOptions<ChunkingOptions> chunkingOptions,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var stats = await service.GetStatsAsync(user.UserId, ct);
            var documents = await service.ListAsync(user.UserId, search: null, ct);
            var chunkedDocuments = documents.Where(d => d.IsChunked).ToList();
            double averageLength = 0;

            if (chunkedDocuments.Count > 0)
            {
                var totals = 0d;
                var count = 0;
                foreach (var document in chunkedDocuments)
                {
                    var documentAverage = await chunks.GetAverageLengthForDocumentAsync(document.Id, ct);
                    if (documentAverage > 0)
                    {
                        totals += documentAverage;
                        count++;
                    }
                }

                averageLength = count > 0 ? totals / count : 0;
            }

            var options = chunkingOptions.Value;
            return Results.Ok(new DocumentChunkingDeveloperDto(
                options.TargetChunkSize,
                options.ChunkOverlap,
                stats.ChunkedCount,
                stats.TotalChunkCount,
                averageLength));
        });

        group.MapGet("/embedding/developer", async (
            IDocumentService service,
            IDocumentChunkRepository chunks,
            IOptions<AiOptions> aiOptions,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var stats = await service.GetStatsAsync(user.UserId, ct);
            var embeddingStats = await chunks.GetEmbeddingStatsForUserAsync(user.UserId, ct);
            var options = aiOptions.Value;

            return Results.Ok(new DocumentEmbeddingDeveloperDto(
                AiProviderCatalog.Google,
                options.Google.EmbeddingModel,
                embeddingStats.VectorDimension,
                embeddingStats.CompletedCount,
                embeddingStats.FailedCount));
        });

        group.MapGet("/search/developer", async (
            IDocumentChunkRepository chunks,
            IOptions<SemanticSearchOptions> searchOptions,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var embeddingStats = await chunks.GetEmbeddingStatsForUserAsync(user.UserId, ct);
            var options = searchOptions.Value;

            return Results.Ok(new SemanticSearchSettingsDeveloperDto(
                options.DefaultTopK,
                options.AllowedTopKValues,
                options.MinSimilarityScorePercent,
                embeddingStats.VectorDimension,
                embeddingStats.CompletedCount));
        });

        group.MapPost("/search", async (
            SemanticSearchRequest request,
            ISemanticSearchService search,
            IOptions<SemanticSearchOptions> searchOptions,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return Results.BadRequest("Query must not be empty.");
            }

            try
            {
                var topK = request.TopK ?? searchOptions.Value.DefaultTopK;
                var result = await search.SearchAsync(user.UserId, request.Query, topK, ct);
                return Results.Ok(ToSearchResponse(result));
            }
            catch (AiServiceException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapGet("/settings", (IDocumentService service) =>
        {
            var settings = service.GetSettings();
            return Results.Ok(new KnowledgeBaseSettingsDto(
                settings.StorageFolder,
                settings.MaxFileSizeBytes,
                settings.DefaultCollection,
                settings.SupportedExtensions,
                settings.FutureExtensions));
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            IDocumentService service,
            IDocumentChunkRepository chunks,
            IOptions<ChunkingOptions> chunkingOptions,
            IOptions<AiOptions> aiOptionsMonitor,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var document = await service.GetAsync(user.UserId, id, ct);
            if (document is null)
            {
                return Results.NotFound();
            }

            var chunkEntities = await chunks.GetForDocumentAsync(id, ct);
            var averageLength = document.IsChunked
                ? await chunks.GetAverageLengthForDocumentAsync(id, ct)
                : 0;
            var options = chunkingOptions.Value;
            var aiOptions = aiOptionsMonitor.Value;

            return Results.Ok(ToDetailDto(document, chunkEntities, options, averageLength, aiOptions));
        });

        group.MapPost("/{id:guid}/chunks/rebuild", async (
            Guid id,
            IDocumentChunkingService chunking,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            try
            {
                var document = await chunking.RebuildChunksAsync(id, user.UserId, ct);
                return Results.Ok(ToDto(document));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapPost("/{id:guid}/embeddings/generate", async (
            Guid id,
            IDocumentEmbeddingService embedding,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            try
            {
                var document = await embedding.GenerateAndPersistAsync(id, user.UserId, ct);
                return Results.Ok(ToDto(document));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (AiServiceException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapPost("/{id:guid}/embeddings/rebuild", async (
            Guid id,
            IDocumentEmbeddingService embedding,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            try
            {
                var document = await embedding.RebuildEmbeddingsAsync(id, user.UserId, ct);
                return Results.Ok(ToDto(document));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (AiServiceException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapPost("/upload", UploadAsync);

        group.MapPut("/{id:guid}/rename", async (
            Guid id,
            RenameDocumentRequest request,
            IDocumentService service,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest("Name must not be empty.");
            }

            try
            {
                var updated = await service.RenameAsync(user.UserId, id, request.Name, ct);
                return updated is null ? Results.NotFound() : Results.Ok(ToDto(updated));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            IDocumentService service,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var deleted = await service.DeleteAsync(user.UserId, id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        group.MapGet("/{id:guid}/open", OpenAsync);

        return app;
    }

    private static async Task<IResult> UploadAsync(
        HttpRequest request,
        IDocumentService service,
        ICurrentUser user,
        CancellationToken ct)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Expected multipart form data.");
        }

        var form = await request.ReadFormAsync(ct);
        var files = form.Files;
        if (files.Count == 0)
        {
            return Results.BadRequest("No files uploaded.");
        }

        var uploaded = new List<DocumentDto>();
        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                continue;
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var document = await service.UploadAsync(
                    user.UserId,
                    file.FileName,
                    stream,
                    file.Length,
                    ct);
                uploaded.Add(ToDto(document));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }

        return uploaded.Count == 1 ? Results.Ok(uploaded[0]) : Results.Ok(uploaded);
    }

    private static async Task<IResult> OpenAsync(
        Guid id,
        IDocumentService service,
        ICurrentUser user,
        CancellationToken ct)
    {
        var result = await service.GetForOpenAsync(user.UserId, id, ct);
        if (result is null)
        {
            return Results.NotFound();
        }

        var (document, absolutePath) = result.Value;
        var contentType = GetContentType(document.Extension);
        return Results.File(absolutePath, contentType, document.FileName, enableRangeProcessing: true);
    }

    private static DocumentDto ToDto(Document document) =>
        new(
            document.Id,
            document.Name,
            document.FileName,
            document.Extension,
            DocumentService.GetTypeLabel(document.Extension),
            document.SizeBytes,
            document.UploadedAt,
            document.UpdatedAt,
            document.IsIndexed,
            document.CollectionId,
            document.TagCount,
            document.ExtractionStatus.ToString(),
            DocumentService.GetExtractionStatusLabel(document.ExtractionStatus),
            document.ExtractionStatus == DocumentExtractionStatus.Completed,
            document.ExtractedLength,
            document.ExtractedAt,
            document.ExtractorUsed,
            document.ExtractionDurationMs,
            document.ExtractionError,
            document.IsChunked,
            document.ChunkCount,
            document.ChunkedAt,
            DocumentService.GetChunkingStatusLabel(document),
            document.IsEmbedded,
            document.EmbeddedChunkCount,
            document.EmbeddingModel,
            document.EmbeddedAt,
            DocumentService.GetEmbeddingStatusLabel(document),
            document.IsSample);

    private static DocumentDetailDto ToDetailDto(
        Document document,
        IReadOnlyList<DocumentChunk> chunks,
        ChunkingOptions options,
        double averageChunkLength,
        AiOptions aiOptions)
    {
        var completed = chunks.Count(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Completed);
        var failed = chunks.Count(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Failed);
        int? dimension = null;
        var sampleVector = chunks.FirstOrDefault(c => c.EmbeddingVector is { Length: > 0 })?.EmbeddingVector;
        if (sampleVector is not null)
        {
            dimension = EmbeddingVectorConverter.FromBytes(sampleVector).Length;
        }

        return new(
            document.Id,
            document.Name,
            document.FileName,
            document.Extension,
            DocumentService.GetTypeLabel(document.Extension),
            document.SizeBytes,
            document.UploadedAt,
            document.UpdatedAt,
            document.ExtractionStatus.ToString(),
            DocumentService.GetExtractionStatusLabel(document.ExtractionStatus),
            document.ExtractedLength,
            document.ExtractedAt,
            document.ExtractorUsed,
            document.ExtractionDurationMs,
            document.ExtractionError,
            DocumentService.BuildTextPreview(document.ExtractedText),
            document.IsChunked,
            document.ChunkCount,
            document.ChunkedAt,
            DocumentService.GetChunkingStatusLabel(document),
            chunks.Select(c => new DocumentChunkPreviewDto(
                c.ChunkIndex,
                DocumentService.BuildChunkPreview(c.Content),
                c.CharacterCount,
                c.PageNumber)).ToList(),
            document.IsChunked
                ? new DocumentChunkDeveloperDto(
                    options.TargetChunkSize,
                    options.ChunkOverlap,
                    document.ChunkCount,
                    averageChunkLength)
                : null,
            document.IsEmbedded,
            document.EmbeddedChunkCount,
            document.EmbeddingModel,
            document.EmbeddedAt,
            DocumentService.GetEmbeddingStatusLabel(document),
            document.IsChunked
                ? new DocumentEmbeddingDeveloperDto(
                    AiProviderCatalog.Google,
                    document.EmbeddingModel ?? aiOptions.Google.EmbeddingModel,
                    dimension,
                    completed,
                    failed)
                : null);
    }

    private static SemanticSearchResponseDto ToSearchResponse(SemanticSearchResultSet result) =>
        new(
            result.Results.Select(r => new SemanticSearchResultDto(
                r.DocumentId,
                r.ChunkId,
                r.Score,
                r.Content,
                DocumentService.BuildChunkPreview(r.Content, maxLength: 160),
                r.FileName,
                r.PageNumber,
                r.ChunkIndex)).ToList(),
            new SemanticSearchDeveloperDto(
                result.Diagnostics.TopK,
                result.Diagnostics.EmbeddingDimensions,
                result.Diagnostics.SearchDurationMs,
                result.Diagnostics.EmbeddingProvider,
                result.Diagnostics.TopScore));

    private static string GetContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            "pdf" => "application/pdf",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "txt" => "text/plain",
            "md" or "markdown" => "text/markdown",
            _ => "application/octet-stream"
        };
}
