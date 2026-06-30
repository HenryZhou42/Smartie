using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Contracts;
using Smartie.Domain.Entities;

namespace Smartie.Api.Endpoints;

public static class FileIntegrationEndpoints
{
    public static IEndpointRouteBuilder MapFileIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/files");

        group.MapGet("/recent", async (IFileIntegrationService files, ICurrentUser user, CancellationToken ct) =>
        {
            var recent = await files.ListRecentAsync(user.UserId, ct);
            return Results.Ok(recent.Select(ToRecentDto));
        });

        group.MapPost("/recent", async (
            RecordRecentFileRequest request,
            IFileIntegrationService files,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.FilePath))
            {
                return Results.BadRequest("FilePath must not be empty.");
            }

            try
            {
                var recorded = await files.RecordRecentAsync(user.UserId, request.FilePath, ct);
                return Results.Ok(ToRecentDto(recorded));
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or InvalidOperationException)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapPut("/recent/{id:guid}/pin", async (
            Guid id,
            PinRecentFileRequest request,
            IFileIntegrationService files,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var updated = await files.PinRecentAsync(user.UserId, id, request.Pinned, ct);
            return updated is null ? Results.NotFound() : Results.Ok(ToRecentDto(updated));
        });

        group.MapPut("/recent/{id:guid}/favorite", async (
            Guid id,
            FavoriteRecentFileRequest request,
            IFileIntegrationService files,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var updated = await files.FavoriteRecentAsync(user.UserId, id, request.IsFavorite, ct);
            return updated is null ? Results.NotFound() : Results.Ok(ToRecentDto(updated));
        });

        group.MapDelete("/recent/{id:guid}", async (
            Guid id,
            IFileIntegrationService files,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var deleted = await files.DeleteRecentAsync(user.UserId, id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        group.MapGet("/favorites", async (IFileIntegrationService files, ICurrentUser user, CancellationToken ct) =>
        {
            var folders = await files.ListFavoriteFoldersAsync(user.UserId, ct);
            return Results.Ok(folders.Select(ToFavoriteDto));
        });

        group.MapPost("/favorites", async (
            AddFavoriteFolderRequest request,
            IFileIntegrationService files,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.FolderPath))
            {
                return Results.BadRequest("FolderPath must not be empty.");
            }

            try
            {
                var created = await files.AddFavoriteFolderAsync(user.UserId, request.FolderPath, request.Label, ct);
                return Results.Ok(ToFavoriteDto(created));
            }
            catch (Exception ex) when (ex is DirectoryNotFoundException)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapDelete("/favorites/{id:guid}", async (
            Guid id,
            IFileIntegrationService files,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var deleted = await files.RemoveFavoriteFolderAsync(user.UserId, id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/search", async (
            FileSearchRequest request,
            IFileIntegrationService files,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return Results.BadRequest("Query must not be empty.");
            }

            var response = await files.SearchAsync(user.UserId, request.Query, ct);
            return Results.Ok(new FileSearchResponseDto(
                response.Results.Select(r => new FileSearchResultDto(
                    r.FilePath,
                    r.FileName,
                    r.Extension,
                    r.Location,
                    r.SizeBytes,
                    r.ModifiedAt)).ToList(),
                new FileSearchDeveloperDto(response.Developer.SearchTimeMs, response.Developer.ResultCount)));
        });

        group.MapGet("/stats", async (IFileIntegrationService files, ICurrentUser user, CancellationToken ct) =>
        {
            var stats = await files.GetStatsAsync(user.UserId, ct);
            return Results.Ok(new FileIntegrationStatsDto(
                stats.RecentFileCount,
                stats.FavoriteFolderCount,
                stats.IndexedDocumentCount,
                stats.RecentFiles.Select(ToRecentDto).ToList(),
                stats.FavoriteFolders.Select(ToFavoriteDto).ToList(),
                stats.RecentlyImported.Select(i => new RecentImportDto(i.DocumentId, i.Name, i.FileName, i.CreatedAt)).ToList()));
        });

        group.MapGet("/settings", async (IFileIntegrationService files, ICurrentUser user, CancellationToken ct) =>
        {
            var settings = await files.GetSettingsAsync(user.UserId, ct);
            return Results.Ok(new FileIntegrationSettingsDto(settings.MaxRecentFiles, settings.ShowHiddenFiles));
        });

        group.MapPut("/settings", async (
            UpdateFileIntegrationSettingsRequest request,
            IFileIntegrationService files,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            await files.UpdateSettingsAsync(
                user.UserId,
                new FileIntegrationSettingsUpdate(request.MaxRecentFiles, request.ShowHiddenFiles),
                ct);
            var settings = await files.GetSettingsAsync(user.UserId, ct);
            return Results.Ok(new FileIntegrationSettingsDto(settings.MaxRecentFiles, settings.ShowHiddenFiles));
        });

        group.MapGet("/developer", async (IFileIntegrationService files, ICurrentUser user, CancellationToken ct) =>
        {
            var stats = await files.GetDeveloperStatsAsync(user.UserId, ct);
            return Results.Ok(new FileIntegrationDeveloperDto(
                stats.FileCount,
                stats.LastSearchTimeMs,
                stats.IndexedFiles,
                stats.FavoriteFolderCount));
        });

        group.MapGet("/supported-types", (IOptions<FileIntegrationOptions> options) =>
            Results.Ok(new SupportedFileTypesDto(options.Value.AllowedExtensions)));

        return app;
    }

    private static RecentFileDto ToRecentDto(RecentFile file) =>
        new(
            file.Id,
            file.FilePath,
            file.FileName,
            file.Extension,
            file.Location,
            file.SizeBytes,
            file.Pinned,
            file.IsFavorite,
            file.LastOpenedAt,
            file.UpdatedAt);

    private static FavoriteFolderDto ToFavoriteDto(FavoriteFolder folder) =>
        new(folder.Id, folder.FolderPath, folder.Label, folder.SortOrder, folder.AddedAt);
}
