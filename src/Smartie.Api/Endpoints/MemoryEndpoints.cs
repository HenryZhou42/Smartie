using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Application.Services;
using Smartie.Contracts;
using Smartie.Domain.Entities;

namespace Smartie.Api.Endpoints;

public static class MemoryEndpoints
{
    public static IEndpointRouteBuilder MapMemoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/memories");

        group.MapGet("/", async (
            string? category,
            bool? pinned,
            IMemoryService memory,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            MemoryCategory? parsedCategory = Enum.TryParse<MemoryCategory>(category, true, out var value)
                ? value
                : null;
            var memories = await memory.ListMemoriesAsync(user.UserId, parsedCategory, pinned, ct);
            return Results.Ok(memories.Select(ToDto));
        });

        group.MapGet("/settings", async (IMemoryService memory, ICurrentUser user, CancellationToken ct) =>
        {
            var settings = await memory.GetSettingsAsync(user.UserId, ct);
            return Results.Ok(new MemorySettingsDto(
                settings.Enabled,
                settings.MaxMemories,
                settings.RetentionDays,
                settings.CurrentCount));
        });

        group.MapPut("/settings", async (
            UpdateMemorySettingsRequest request,
            IMemoryService memory,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            await memory.UpdateSettingsAsync(
                user.UserId,
                new MemorySettingsUpdate(request.Enabled, request.MaxMemories, request.RetentionDays),
                ct);
            var settings = await memory.GetSettingsAsync(user.UserId, ct);
            return Results.Ok(new MemorySettingsDto(
                settings.Enabled,
                settings.MaxMemories,
                settings.RetentionDays,
                settings.CurrentCount));
        });

        group.MapGet("/developer", async (IMemoryService memory, ICurrentUser user, CancellationToken ct) =>
        {
            var stats = await memory.GetDeveloperStatsAsync(user.UserId, ct);
            return Results.Ok(new MemoryDeveloperDto(
                stats.MemoryCount,
                stats.PinnedCount,
                stats.EmbeddingDimensions,
                stats.DefaultSearchTopK,
                stats.MinSimilarityScorePercent));
        });

        group.MapPost("/search", async (
            MemorySearchRequest request,
            IMemoryService memory,
            Microsoft.Extensions.Options.IOptions<MemoryOptions> options,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return Results.BadRequest("Query must not be empty.");
            }

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var topK = request.TopK ?? options.Value.DefaultSearchTopK;
                var settings = await memory.GetSettingsAsync(user.UserId, ct);
                var results = await memory.SearchMemoryAsync(user.UserId, request.Query, topK, ct);
                stopwatch.Stop();
                return Results.Ok(new MemorySearchResponseDto(
                    results.Select(r => new MemorySearchResultDto(
                        r.MemoryId,
                        r.Content,
                        r.Category.ToString(),
                        r.Importance.ToString(),
                        r.Score,
                        r.Pinned,
                        r.CreatedAt,
                        r.LastReferencedAt)).ToList(),
                    new MemorySearchDeveloperDto(
                        results.Count,
                        results.Count,
                        results.Count > 0 ? results[0].Score : null,
                        stopwatch.ElapsedMilliseconds,
                        settings.CurrentCount)));
            }
            catch (AiServiceException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapPost("/", async (
            CreateMemoryRequest request,
            IMemoryService memory,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest("Content must not be empty.");
            }

            if (!TryParseCategory(request.Category, out var category))
            {
                return Results.BadRequest($"Unknown category '{request.Category}'.");
            }

            if (!TryParseImportance(request.Importance, out var importance))
            {
                return Results.BadRequest($"Unknown importance '{request.Importance}'.");
            }

            try
            {
                var created = await memory.StoreMemoryAsync(user.UserId, request.Content, category, importance, ct);
                return Results.Ok(ToDto(created));
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

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateMemoryRequest request,
            IMemoryService memory,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest("Content must not be empty.");
            }

            if (!TryParseCategory(request.Category, out var category))
            {
                return Results.BadRequest($"Unknown category '{request.Category}'.");
            }

            if (!TryParseImportance(request.Importance, out var importance))
            {
                return Results.BadRequest($"Unknown importance '{request.Importance}'.");
            }

            try
            {
                var updated = await memory.UpdateMemoryAsync(user.UserId, id, request.Content, category, importance, ct);
                return updated is null ? Results.NotFound() : Results.Ok(ToDto(updated));
            }
            catch (AiServiceException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapPut("/{id:guid}/pin", async (
            Guid id,
            PinMemoryRequest request,
            IMemoryService memory,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var updated = await memory.PinMemoryAsync(user.UserId, id, request.Pinned, ct);
            return updated is null ? Results.NotFound() : Results.Ok(ToDto(updated));
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            IMemoryService memory,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var deleted = await memory.DeleteMemoryAsync(user.UserId, id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }

    private static MemoryDto ToDto(Memory memory) =>
        new(
            memory.Id,
            memory.Content,
            memory.Category.ToString(),
            memory.Importance.ToString(),
            memory.Pinned,
            memory.CreatedAt,
            memory.UpdatedAt,
            memory.LastReferencedAt);

    private static bool TryParseCategory(string value, out MemoryCategory category) =>
        Enum.TryParse(value, ignoreCase: true, out category);

    private static bool TryParseImportance(string value, out MemoryImportance importance) =>
        Enum.TryParse(value, ignoreCase: true, out importance);
}
