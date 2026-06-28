using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Smartie.Contracts;

namespace Smartie.Shared.Services;

/// <summary>
/// Typed client over the Smartie backend API. The client holds no AI keys; it
/// only speaks DTOs to the server.
/// </summary>
public sealed class SmartieApiClient
{
    private const string DataPrefix = "data: ";

    private readonly HttpClient _http;
    private readonly ISmartieApiEndpointProvider _endpointProvider;
    private Uri? _cachedBaseAddress;

    public SmartieApiClient(HttpClient http, ISmartieApiEndpointProvider endpointProvider)
    {
        _http = http;
        _endpointProvider = endpointProvider;
    }

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<ConversationDto>>(await UrlAsync("api/conversations", ct), ct)
            .ConfigureAwait(false)
        ?? new List<ConversationDto>();

    public async Task<ConversationDto> CreateConversationAsync(string? title = null, CancellationToken ct = default)
    {
        var response = await _http
            .PostAsJsonAsync(await UrlAsync("api/conversations", ct), new CreateConversationRequest(title), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid conversationId, CancellationToken ct = default) =>
        await _http
            .GetFromJsonAsync<List<MessageDto>>(await UrlAsync($"api/conversations/{conversationId}/messages", ct), ct)
            .ConfigureAwait(false)
        ?? new List<MessageDto>();

    public async Task<MessageDto> SendMessageAsync(Guid conversationId, string content, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
                await UrlAsync($"api/conversations/{conversationId}/messages", ct),
                new SendMessageRequest(content),
                ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MessageDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task SetConversationPinnedAsync(Guid conversationId, bool isPinned, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(
                await UrlAsync($"api/conversations/{conversationId}/pin", ct),
                new SetConversationPinnedRequest(isPinned),
                ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var response = await _http
            .DeleteAsync(await UrlAsync($"api/conversations/{conversationId}", ct), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AiSettingsDto> GetAiSettingsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<AiSettingsDto>(await UrlAsync("api/settings/ai", ct), ct).ConfigureAwait(false)
        ?? new AiSettingsDto("google", Array.Empty<AiProviderDto>());

    public async Task SetAiProviderAsync(string provider, CancellationToken ct = default)
    {
        var response = await _http
            .PutAsJsonAsync(await UrlAsync("api/settings/ai/provider", ct), new SelectProviderRequest(provider), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveAiProviderCredentialAsync(
        string provider,
        SaveProviderCredentialRequest request,
        CancellationToken ct = default)
    {
        var response = await _http
            .PutAsJsonAsync(await UrlAsync($"api/settings/ai/providers/{provider}", ct), request, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Streams the assistant reply as text deltas over Server-Sent Events.</summary>
    public IAsyncEnumerable<string> StreamMessageAsync(
        Guid conversationId,
        string content,
        IReadOnlyList<Guid>? documentIds = null,
        IReadOnlyList<Guid>? stagingAttachmentIds = null,
        CancellationToken ct = default) =>
        StreamSseAsync(
            $"api/conversations/{conversationId}/messages/stream",
            HttpMethod.Post,
            new SendMessageRequest(content, documentIds, stagingAttachmentIds),
            ct);

    public async Task<StagingChatAttachmentDto> StageChatAttachmentAsync(
        Guid conversationId,
        Stream stream,
        string fileName,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(stream);
        content.Add(streamContent, "file", fileName);

        var response = await _http
            .PostAsync(await UrlAsync($"api/conversations/{conversationId}/attachments/staging", ct), content, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                ? "Could not upload attachment."
                : message);
        }

        return (await response.Content.ReadFromJsonAsync<StagingChatAttachmentDto>(cancellationToken: ct)
            .ConfigureAwait(false))!;
    }

    public async Task DeleteStagingChatAttachmentAsync(
        Guid conversationId,
        Guid stagingId,
        CancellationToken ct = default)
    {
        var response = await _http
            .DeleteAsync(await UrlAsync($"api/conversations/{conversationId}/attachments/staging/{stagingId}", ct), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Edits a user message and truncates all following turns.</summary>
    public async Task<IReadOnlyList<MessageDto>> EditMessageAsync(
        Guid conversationId,
        Guid messageId,
        string content,
        CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
                await UrlAsync($"api/conversations/{conversationId}/messages/{messageId}/edit", ct),
                new EditMessageRequest(content),
                ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<MessageDto>>(cancellationToken: ct).ConfigureAwait(false)
            ?? new List<MessageDto>();
    }

    /// <summary>Streams a new assistant reply from the current conversation history.</summary>
    public IAsyncEnumerable<string> StreamRegenerateAsync(
        Guid conversationId,
        CancellationToken ct = default) =>
        StreamSseAsync(
            $"api/conversations/{conversationId}/messages/regenerate/stream",
            HttpMethod.Post,
            new { },
            ct);

    /// <summary>Edits a user message, truncates later turns, and streams a new assistant reply.</summary>
    public IAsyncEnumerable<string> StreamEditMessageAsync(
        Guid conversationId,
        Guid messageId,
        string content,
        CancellationToken ct = default) =>
        StreamSseAsync(
            $"api/conversations/{conversationId}/messages/{messageId}/edit/stream",
            HttpMethod.Post,
            new EditMessageRequest(content),
            ct);

    private async IAsyncEnumerable<string> StreamSseAsync(
        string path,
        HttpMethod method,
        object body,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, await UrlAsync(path, ct))
        {
            Content = JsonContent.Create(body)
        };

        using var response = await _http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                continue;
            }

            if (!line.StartsWith(DataPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var json = line[DataPrefix.Length..];
            string? delta = null;
            try
            {
                delta = JsonSerializer.Deserialize<string>(json);
            }
            catch (JsonException)
            {
                // Ignore malformed/keep-alive frames.
            }

            if (!string.IsNullOrEmpty(delta))
            {
                yield return delta;
            }
        }
    }

    public async Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(string? search = null, CancellationToken ct = default)
    {
        var path = string.IsNullOrWhiteSpace(search)
            ? "api/documents"
            : $"api/documents?search={Uri.EscapeDataString(search)}";
        return await _http.GetFromJsonAsync<List<DocumentDto>>(await UrlAsync(path, ct), ct).ConfigureAwait(false)
            ?? new List<DocumentDto>();
    }

    public async Task<DocumentStatsDto> GetDocumentStatsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<DocumentStatsDto>(await UrlAsync("api/documents/stats", ct), ct)
            .ConfigureAwait(false)
        ?? new DocumentStatsDto(0, 0, 0, 0, 0, 0, 0, null, null);

    public async Task<DocumentDetailDto?> GetDocumentDetailAsync(Guid id, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<DocumentDetailDto>(await UrlAsync($"api/documents/{id}", ct), ct)
            .ConfigureAwait(false);

    public async Task<DocumentExtractionDeveloperDto?> GetDocumentExtractionDeveloperAsync(CancellationToken ct = default) =>
        await _http
            .GetFromJsonAsync<DocumentExtractionDeveloperDto>(await UrlAsync("api/documents/extraction/developer", ct), ct)
            .ConfigureAwait(false);

    public async Task<KnowledgeBaseSettingsDto> GetKnowledgeBaseSettingsAsync(CancellationToken ct = default) =>
        await _http
            .GetFromJsonAsync<KnowledgeBaseSettingsDto>(await UrlAsync("api/documents/settings", ct), ct)
            .ConfigureAwait(false)
        ?? new KnowledgeBaseSettingsDto(string.Empty, 0, null, Array.Empty<string>(), Array.Empty<string>());

    public async Task<DocumentDto> UploadDocumentAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(stream);
        content.Add(streamContent, "file", fileName);

        var response = await _http
            .PostAsync(await UrlAsync("api/documents/upload", ct), content, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<DocumentDto> RenameDocumentAsync(Guid id, string name, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(
                await UrlAsync($"api/documents/{id}/rename", ct),
                new RenameDocumentRequest(name),
                ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task DeleteDocumentAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync(await UrlAsync($"api/documents/{id}", ct), ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Uri> GetDocumentOpenUriAsync(Guid id, CancellationToken ct = default) =>
        await UrlAsync($"api/documents/{id}/open", ct);

    private async Task<Uri> UrlAsync(string path, CancellationToken cancellationToken)
    {
        var baseAddress = _cachedBaseAddress
            ??= await _endpointProvider.GetBaseAddressAsync(cancellationToken).ConfigureAwait(false);
        return new Uri(baseAddress, path);
    }
}
