using Smartie.Application.Abstractions;
using Smartie.Contracts;

namespace Smartie.Api.Endpoints;

public static class CommandPaletteEndpoints
{
    public static IEndpointRouteBuilder MapCommandPaletteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/commands");

        group.MapPost("/search", async (
            CommandSearchRequest request,
            ICommandPaletteService palette,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var response = await palette.SearchAsync(user.UserId, request.Query, ct);
            return Results.Ok(ToDto(response));
        });

        group.MapPost("/usage", async (
            RecordCommandUsageRequest request,
            ICommandPaletteService palette,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.CommandId))
            {
                return Results.BadRequest("CommandId must not be empty.");
            }

            await palette.RecordUsageAsync(user.UserId, request.CommandId, ct);
            return Results.NoContent();
        });

        group.MapGet("/developer", async (
            ICommandPaletteService palette,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var stats = await palette.GetDeveloperStatsAsync(user.UserId, ct);
            return Results.Ok(new CommandPaletteDeveloperDto(
                stats.CommandCount,
                stats.SearchLatencyMs,
                stats.TopRankingScore));
        });

        return app;
    }

    private static CommandSearchResponseDto ToDto(CommandSearchResponse response) =>
        new(
            response.Results.Select(r => new CommandDto(
                r.Id,
                r.Title,
                r.Subtitle,
                r.Icon,
                r.Shortcut,
                r.Route,
                r.Enabled,
                r.RankingScore,
                r.UsageCount,
                r.LastUsed)).ToList(),
            new CommandPaletteDeveloperDto(
                response.Developer.CommandCount,
                response.Developer.SearchLatencyMs,
                response.Developer.TopRankingScore));
}
