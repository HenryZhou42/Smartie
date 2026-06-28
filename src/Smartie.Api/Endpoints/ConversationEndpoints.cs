using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Smartie.Application.Abstractions;
using Smartie.Contracts;
using Smartie.Domain.Entities;

namespace Smartie.Api.Endpoints;

/// <summary>
/// Minimal-API endpoints for conversations and messages, including an SSE
/// streaming variant for assistant replies.
/// </summary>
public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conversations");

        group.MapGet("/", async (IConversationService service, ICurrentUser user, CancellationToken ct) =>
        {
            var conversations = await service.ListAsync(user.UserId, ct);
            return Results.Ok(conversations.Select(ToDto));
        });

        group.MapPost("/", async (
            CreateConversationRequest? request,
            IConversationService service,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var conversation = await service.CreateAsync(user.UserId, request?.Title, ct);
            return Results.Created($"/api/conversations/{conversation.Id}", ToDto(conversation));
        });

        group.MapGet("/{id:guid}/messages", async (Guid id, IConversationService service, CancellationToken ct) =>
        {
            var conversation = await service.GetAsync(id, ct);
            return conversation is null
                ? Results.NotFound()
                : Results.Ok(conversation.Messages.Select(ToDto));
        });

        group.MapPost("/{id:guid}/messages", async (
            Guid id,
            SendMessageRequest request,
            IConversationService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest("Message content must not be empty.");
            }

            if (await service.GetAsync(id, ct) is null)
            {
                return Results.NotFound();
            }

            var reply = await service.SendAsync(id, request.Content, ct);
            return Results.Ok(ToDto(reply));
        });

        group.MapPost("/{id:guid}/messages/stream", StreamReplyAsync);

        group.MapPost("/{id:guid}/attachments/staging", UploadStagingAsync);
        group.MapDelete("/{id:guid}/attachments/staging/{stagingId:guid}", DeleteStagingAsync);

        group.MapPut("/{id:guid}/pin", async (
            Guid id,
            SetConversationPinnedRequest request,
            IConversationService service,
            CancellationToken ct) =>
        {
            if (await service.GetAsync(id, ct) is null)
            {
                return Results.NotFound();
            }

            await service.SetPinnedAsync(id, request.IsPinned, ct);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/messages/{messageId:guid}/edit/stream", StreamEditAsync);

        group.MapPost("/{id:guid}/messages/{messageId:guid}/edit", EditMessageAsync);

        group.MapPost("/{id:guid}/messages/regenerate/stream", StreamRegenerateAsync);

        group.MapDelete("/{id:guid}", async (Guid id, IConversationService service, CancellationToken ct) =>
        {
            var deleted = await service.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }

    private static async Task StreamReplyAsync(
        Guid id,
        SendMessageRequest request,
        IConversationService service,
        HttpContext http,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsync("Message content must not be empty.", ct);
            return;
        }

        if (await service.GetAsync(id, ct) is null)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await WriteSseStreamAsync(
            http,
            service.StreamReplyAsync(id, request.Content, request.DocumentIds, request.StagingAttachmentIds, ct),
            ct);
    }

    private static async Task<IResult> UploadStagingAsync(
        Guid id,
        HttpRequest request,
        IChatAttachmentService chatAttachments,
        ICurrentUser user,
        IConversationService conversations,
        CancellationToken ct)
    {
        if (await conversations.GetAsync(id, ct) is null)
        {
            return Results.NotFound();
        }

        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Expected multipart form data.");
        }

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest("A non-empty file is required.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var staged = await chatAttachments.StageUploadAsync(
                user.UserId,
                id,
                file.FileName,
                stream,
                file.Length,
                ct);
            return Results.Ok(new StagingChatAttachmentDto(
                staged.StagingId,
                staged.FileName,
                staged.Extension,
                staged.TypeLabel,
                staged.SizeBytes));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> DeleteStagingAsync(
        Guid id,
        Guid stagingId,
        IChatAttachmentService chatAttachments,
        ICurrentUser user,
        IConversationService conversations,
        CancellationToken ct)
    {
        if (await conversations.GetAsync(id, ct) is null)
        {
            return Results.NotFound();
        }

        try
        {
            await chatAttachments.DeleteStagingAsync(user.UserId, id, stagingId, ct);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> EditMessageAsync(
        Guid id,
        Guid messageId,
        EditMessageRequest request,
        IConversationService service,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Results.BadRequest("Message content must not be empty.");
        }

        if (await service.GetAsync(id, ct) is null)
        {
            return Results.NotFound();
        }

        try
        {
            var messages = await service.EditUserMessageAsync(id, messageId, request.Content, ct);
            return Results.Ok(messages.Select(ToDto));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task StreamRegenerateAsync(
        Guid id,
        IConversationService service,
        HttpContext http,
        CancellationToken ct)
    {
        if (await service.GetAsync(id, ct) is null)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await WriteSseStreamAsync(http, service.StreamRegenerateAsync(id, ct), ct);
    }

    private static async Task StreamEditAsync(
        Guid id,
        Guid messageId,
        EditMessageRequest request,
        IConversationService service,
        HttpContext http,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsync("Message content must not be empty.", ct);
            return;
        }

        if (await service.GetAsync(id, ct) is null)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        try
        {
            await WriteSseStreamAsync(
                http,
                service.StreamEditAndRegenerateAsync(id, messageId, request.Content, ct),
                ct);
        }
        catch (KeyNotFoundException)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
        }
        catch (InvalidOperationException ex)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsync(ex.Message, ct);
        }
    }

    private static async Task WriteSseStreamAsync(
        HttpContext http,
        IAsyncEnumerable<string> stream,
        CancellationToken ct)
    {
        http.Response.Headers.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        await foreach (var chunk in stream.WithCancellation(ct))
        {
            await http.Response.WriteAsync($"data: {JsonSerializer.Serialize(chunk)}\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
        }

        await http.Response.WriteAsync("event: done\ndata: \"\"\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }

    private static ConversationDto ToDto(Conversation c) =>
        new(c.Id, c.Title, c.CreatedAt, c.UpdatedAt, c.IsPinned, c.PinnedAt);

    private static MessageDto ToDto(Message m) =>
        new(
            m.Id,
            m.Role.ToString().ToLowerInvariant(),
            m.Content,
            m.CreatedAt,
            m.UpdatedAt,
            m.IsEdited,
            m.EditedAt,
            m.Role == MessageRole.Assistant && m.GenerationStatus == MessageGenerationStatus.Stopped
                ? "stopped"
                : null,
            m.Attachments
                .OrderBy(a => a.CreatedAt)
                .ThenBy(a => a.Id)
                .Select(ToAttachmentDto)
                .ToList());

    private static MessageAttachmentDto ToAttachmentDto(MessageAttachment attachment)
    {
        var sourceType = attachment.SourceType == MessageAttachmentSourceType.KnowledgeBase
            ? "KnowledgeBase"
            : "LocalUpload";
        var sourceLabel = attachment.SourceType == MessageAttachmentSourceType.KnowledgeBase
            ? "Knowledge Base"
            : "Local Upload";
        var name = attachment.Document?.Name
            ?? Path.GetFileNameWithoutExtension(attachment.OriginalFileName);

        return new MessageAttachmentDto(
            attachment.Id,
            attachment.DocumentId,
            name,
            attachment.OriginalFileName,
            attachment.Extension,
            attachment.SizeBytes,
            sourceType,
            sourceLabel);
    }
}
