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

        // Uses only a synthetic prompt; never stores a conversation or sends library/memory data.
        group.MapPost("/test", async (IChatAiService chat, CancellationToken ct) =>
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                await foreach (var delta in chat.StreamReplyAsync(
                    [new Smartie.Domain.Entities.Message
                    {
                        Role = Smartie.Domain.Entities.MessageRole.User,
                        Content = "Reply with OK only."
                    }], timeout.Token))
                {
                    if (!string.IsNullOrWhiteSpace(delta))
                        return Results.Ok("Connection successful. You can start chatting.");
                }
                return Results.BadRequest("The provider returned no text. Check the model and try again.");
            }
            catch (OperationCanceledException)
            {
                return Results.BadRequest("Connection timed out or was cancelled. Check your network and local model server.");
            }
            catch (Exception)
            {
                // Provider exceptions can contain request URLs, credentials, or response bodies.
                return Results.BadRequest("Connection failed. Check your saved API key, model, endpoint, network, and provider quota.");
            }
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
