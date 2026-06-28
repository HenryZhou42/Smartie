using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Contracts;

namespace Smartie.Api.Endpoints;

/// <summary>
/// Endpoints for reading and updating the current user's AI provider settings
/// (Community / "Bring Your Own AI" edition).
/// </summary>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings/ai");

        group.MapGet("/", async (IAiSettingsService settings, ICurrentUser user, CancellationToken ct) =>
        {
            var snapshot = await settings.GetSnapshotAsync(user.UserId, ct);
            return Results.Ok(ToDto(snapshot));
        });

        group.MapPut("/provider", async (
            SelectProviderRequest request,
            IAiSettingsService settings,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (!AiProviderCatalog.IsKnown(request.Provider))
            {
                return Results.BadRequest($"Unknown provider '{request.Provider}'.");
            }

            try
            {
                await settings.SetSelectedProviderAsync(user.UserId, request.Provider, ct);
                return Results.NoContent();
            }
            catch (AiServiceException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapPut("/providers/{provider}", async (
            string provider,
            SaveProviderCredentialRequest request,
            IAiSettingsService settings,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (!AiProviderCatalog.IsKnown(provider))
            {
                return Results.BadRequest($"Unknown provider '{provider}'.");
            }

            await settings.SaveCredentialAsync(user.UserId, provider, request.ApiKey, request.ChatModel, request.Endpoint, ct);
            return Results.NoContent();
        });

        return app;
    }

    private static AiSettingsDto ToDto(AiSettingsSnapshot snapshot) =>
        new(
            snapshot.SelectedProvider,
            snapshot.Providers.Select(p => new AiProviderDto(
                p.Info.Key,
                p.Info.DisplayName,
                p.Info.Available,
                p.Info.RequiresApiKey,
                p.Info.RequiresEndpoint,
                p.HasApiKey,
                p.ChatModel ?? p.Info.DefaultChatModel,
                p.Endpoint,
                p.Info.DefaultChatModel,
                p.Info.DefaultEndpoint)).ToList());
}
