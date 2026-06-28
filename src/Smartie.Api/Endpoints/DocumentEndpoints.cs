using Smartie.Application.Abstractions;
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
                stats.LastExtractorUsed));
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
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var document = await service.GetAsync(user.UserId, id, ct);
            return document is null ? Results.NotFound() : Results.Ok(ToDetailDto(document));
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
            document.ExtractionError);

    private static DocumentDetailDto ToDetailDto(Document document) =>
        new(
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
            DocumentService.BuildTextPreview(document.ExtractedText));

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
