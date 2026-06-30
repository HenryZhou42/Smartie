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
        ?? new DocumentStatsDto(0, 0, 0, 0, 0, 0, 0, null, null, 0, 0, 0, 0);

    public async Task<DocumentEmbeddingDeveloperDto?> GetDocumentEmbeddingDeveloperAsync(CancellationToken ct = default) =>
        await _http
            .GetFromJsonAsync<DocumentEmbeddingDeveloperDto>(await UrlAsync("api/documents/embedding/developer", ct), ct)
            .ConfigureAwait(false);

    public async Task<SemanticSearchSettingsDeveloperDto?> GetSemanticSearchDeveloperAsync(CancellationToken ct = default) =>
        await _http
            .GetFromJsonAsync<SemanticSearchSettingsDeveloperDto>(await UrlAsync("api/documents/search/developer", ct), ct)
            .ConfigureAwait(false);

    public async Task<SemanticSearchResponseDto> SearchKnowledgeBaseAsync(
        string query,
        int? topK = null,
        CancellationToken ct = default)
    {
        var response = await _http
            .PostAsJsonAsync(await UrlAsync("api/documents/search", ct), new SemanticSearchRequest(query, topK), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SemanticSearchResponseDto>(cancellationToken: ct)
            .ConfigureAwait(false))!;
    }

    public async Task<DocumentChunkingDeveloperDto?> GetDocumentChunkingDeveloperAsync(CancellationToken ct = default) =>
        await _http
            .GetFromJsonAsync<DocumentChunkingDeveloperDto>(await UrlAsync("api/documents/chunking/developer", ct), ct)
            .ConfigureAwait(false);

    public async Task<DocumentDto> RebuildDocumentChunksAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(await UrlAsync($"api/documents/{id}/chunks/rebuild", ct), null, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<DocumentDto> GenerateDocumentEmbeddingsAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(await UrlAsync($"api/documents/{id}/embeddings/generate", ct), null, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<DocumentDto> RebuildDocumentEmbeddingsAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(await UrlAsync($"api/documents/{id}/embeddings/rebuild", ct), null, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

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

    public async Task<IReadOnlyList<MemoryDto>> GetMemoriesAsync(string? category = null, bool? pinned = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(category))
        {
            query.Add($"category={Uri.EscapeDataString(category)}");
        }

        if (pinned is true)
        {
            query.Add("pinned=true");
        }

        var path = query.Count > 0 ? $"api/memories?{string.Join("&", query)}" : "api/memories";
        return await _http.GetFromJsonAsync<List<MemoryDto>>(await UrlAsync(path, ct), ct).ConfigureAwait(false)
            ?? new List<MemoryDto>();
    }

    public async Task<MemorySettingsDto> GetMemorySettingsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<MemorySettingsDto>(await UrlAsync("api/memories/settings", ct), ct).ConfigureAwait(false)
        ?? new MemorySettingsDto(true, 200, 365, 0);

    public async Task<MemorySettingsDto> UpdateMemorySettingsAsync(UpdateMemorySettingsRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(await UrlAsync("api/memories/settings", ct), request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MemorySettingsDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<MemoryDeveloperDto?> GetMemoryDeveloperAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<MemoryDeveloperDto>(await UrlAsync("api/memories/developer", ct), ct).ConfigureAwait(false);

    public async Task<MemorySearchResponseDto> SearchMemoriesAsync(string query, int? topK = null, CancellationToken ct = default)
    {
        var response = await _http
            .PostAsJsonAsync(await UrlAsync("api/memories/search", ct), new MemorySearchRequest(query, topK), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MemorySearchResponseDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<MemoryDto> CreateMemoryAsync(string content, string category, string importance, CancellationToken ct = default)
    {
        var response = await _http
            .PostAsJsonAsync(await UrlAsync("api/memories", ct), new CreateMemoryRequest(content, category, importance), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MemoryDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<MemoryDto> UpdateMemoryAsync(Guid id, string content, string category, string importance, CancellationToken ct = default)
    {
        var response = await _http
            .PutAsJsonAsync(await UrlAsync($"api/memories/{id}", ct), new UpdateMemoryRequest(content, category, importance), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MemoryDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<MemoryDto> SetMemoryPinnedAsync(Guid id, bool pinned, CancellationToken ct = default)
    {
        var response = await _http
            .PutAsJsonAsync(await UrlAsync($"api/memories/{id}/pin", ct), new PinMemoryRequest(pinned), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MemoryDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task DeleteMemoryAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync(await UrlAsync($"api/memories/{id}", ct), ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CommandSearchResponseDto> SearchCommandsAsync(string? query, CancellationToken ct = default)
    {
        var response = await _http
            .PostAsJsonAsync(await UrlAsync("api/commands/search", ct), new CommandSearchRequest(query), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommandSearchResponseDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task RecordCommandUsageAsync(string commandId, CancellationToken ct = default)
    {
        var response = await _http
            .PostAsJsonAsync(await UrlAsync("api/commands/usage", ct), new RecordCommandUsageRequest(commandId), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CommandPaletteDeveloperDto?> GetCommandPaletteDeveloperAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<CommandPaletteDeveloperDto>(await UrlAsync("api/commands/developer", ct), ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<TaskDto>> GetTasksAsync(
        string? view = null,
        string? search = null,
        string? sort = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(view))
        {
            query.Add($"view={Uri.EscapeDataString(view)}");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (!string.IsNullOrWhiteSpace(sort))
        {
            query.Add($"sort={Uri.EscapeDataString(sort)}");
        }

        var path = query.Count > 0 ? $"api/tasks?{string.Join("&", query)}" : "api/tasks";
        return await _http.GetFromJsonAsync<List<TaskDto>>(await UrlAsync(path, ct), ct).ConfigureAwait(false)
            ?? new List<TaskDto>();
    }

    public async Task<TaskStatsDto> GetTaskStatsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<TaskStatsDto>(await UrlAsync("api/tasks/stats", ct), ct).ConfigureAwait(false)
        ?? new TaskStatsDto(0, 0, 0, 0, 0, 0, Array.Empty<TaskDto>());

    public async Task<TaskSettingsDto> GetTaskSettingsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<TaskSettingsDto>(await UrlAsync("api/tasks/settings", ct), ct).ConfigureAwait(false)
        ?? new TaskSettingsDto("DueDate", "Medium", true);

    public async Task<TaskSettingsDto> UpdateTaskSettingsAsync(UpdateTaskSettingsRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(await UrlAsync("api/tasks/settings", ct), request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskSettingsDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<TaskDeveloperDto?> GetTaskDeveloperAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<TaskDeveloperDto>(await UrlAsync("api/tasks/developer", ct), ct).ConfigureAwait(false);

    public async Task<TaskDto> CreateTaskAsync(CreateTaskRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(await UrlAsync("api/tasks", ct), request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<TaskDto> UpdateTaskAsync(Guid id, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(await UrlAsync($"api/tasks/{id}", ct), request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<TaskDto> CompleteTaskAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.PutAsync(await UrlAsync($"api/tasks/{id}/complete", ct), null, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<TaskDto> SetTaskPinnedAsync(Guid id, bool pinned, CancellationToken ct = default)
    {
        var response = await _http
            .PutAsJsonAsync(await UrlAsync($"api/tasks/{id}/pin", ct), new PinTaskRequest(pinned), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<TaskDto> SetTaskArchivedAsync(Guid id, bool archived, CancellationToken ct = default)
    {
        var response = await _http
            .PutAsJsonAsync(await UrlAsync($"api/tasks/{id}/archive", ct), new ArchiveTaskRequest(archived), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task DeleteTaskAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync(await UrlAsync($"api/tasks/{id}", ct), ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<RecentFileDto>> GetRecentFilesAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<RecentFileDto>>(await UrlAsync("api/files/recent", ct), ct).ConfigureAwait(false)
        ?? new List<RecentFileDto>();

    public async Task<RecentFileDto> RecordRecentFileAsync(string filePath, CancellationToken ct = default)
    {
        var response = await _http
            .PostAsJsonAsync(await UrlAsync("api/files/recent", ct), new RecordRecentFileRequest(filePath), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RecentFileDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<RecentFileDto> SetRecentFilePinnedAsync(Guid id, bool pinned, CancellationToken ct = default)
    {
        var response = await _http
            .PutAsJsonAsync(await UrlAsync($"api/files/recent/{id}/pin", ct), new PinRecentFileRequest(pinned), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RecentFileDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<RecentFileDto> SetRecentFileFavoriteAsync(Guid id, bool favorite, CancellationToken ct = default)
    {
        var response = await _http
            .PutAsJsonAsync(await UrlAsync($"api/files/recent/{id}/favorite", ct), new FavoriteRecentFileRequest(favorite), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RecentFileDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task DeleteRecentFileAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync(await UrlAsync($"api/files/recent/{id}", ct), ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<FavoriteFolderDto>> GetFavoriteFoldersAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<FavoriteFolderDto>>(await UrlAsync("api/files/favorites", ct), ct).ConfigureAwait(false)
        ?? new List<FavoriteFolderDto>();

    public async Task<FavoriteFolderDto> AddFavoriteFolderAsync(string folderPath, string? label = null, CancellationToken ct = default)
    {
        var response = await _http
            .PostAsJsonAsync(await UrlAsync("api/files/favorites", ct), new AddFavoriteFolderRequest(folderPath, label), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FavoriteFolderDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task DeleteFavoriteFolderAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync(await UrlAsync($"api/files/favorites/{id}", ct), ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<FileSearchResponseDto> SearchFilesAsync(string query, CancellationToken ct = default)
    {
        var response = await _http
            .PostAsJsonAsync(await UrlAsync("api/files/search", ct), new FileSearchRequest(query), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FileSearchResponseDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<FileIntegrationStatsDto> GetFileIntegrationStatsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<FileIntegrationStatsDto>(await UrlAsync("api/files/stats", ct), ct).ConfigureAwait(false)
        ?? new FileIntegrationStatsDto(0, 0, 0, Array.Empty<RecentFileDto>(), Array.Empty<FavoriteFolderDto>(), Array.Empty<RecentImportDto>());

    public async Task<FileIntegrationSettingsDto> GetFileIntegrationSettingsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<FileIntegrationSettingsDto>(await UrlAsync("api/files/settings", ct), ct).ConfigureAwait(false)
        ?? new FileIntegrationSettingsDto(50, false);

    public async Task<FileIntegrationSettingsDto> UpdateFileIntegrationSettingsAsync(
        UpdateFileIntegrationSettingsRequest request,
        CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(await UrlAsync("api/files/settings", ct), request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FileIntegrationSettingsDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<FileIntegrationDeveloperDto?> GetFileIntegrationDeveloperAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<FileIntegrationDeveloperDto>(await UrlAsync("api/files/developer", ct), ct).ConfigureAwait(false);

    public async Task<AppearanceSettingsDto> GetAppearanceSettingsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<AppearanceSettingsDto>(await UrlAsync("api/appearance/settings", ct), ct).ConfigureAwait(false)
        ?? new AppearanceSettingsDto(
            "Default", "Purple", null, "Default", "Medium", "Expanded", "Enabled", false, "Disabled",
            20, 200, "Medium", "Standard", "Normal", "Default", "Default", DateTimeOffset.UtcNow);

    public async Task<AppearanceSettingsDto> UpdateAppearanceSettingsAsync(
        UpdateAppearanceSettingsRequest request,
        CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(await UrlAsync("api/appearance/settings", ct), request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AppearanceSettingsDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<AppearanceOptionsDto?> GetAppearanceOptionsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<AppearanceOptionsDto>(await UrlAsync("api/appearance/options", ct), ct).ConfigureAwait(false);

    public async Task<AppearanceDeveloperDto?> GetAppearanceDeveloperAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<AppearanceDeveloperDto>(await UrlAsync("api/appearance/developer", ct), ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<PluginDto>> GetPluginsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<PluginDto>>(await UrlAsync("api/plugins", ct), ct).ConfigureAwait(false)
        ?? [];

    public async Task<PluginScanResultDto> ScanPluginsAsync(CancellationToken ct = default) =>
        await (await _http.PostAsync(await UrlAsync("api/plugins/scan", ct), null, ct).ConfigureAwait(false))
            .Content.ReadFromJsonAsync<PluginScanResultDto>(cancellationToken: ct).ConfigureAwait(false)
        ?? new PluginScanResultDto(0, 0, []);

    public async Task<PluginDto> EnablePluginAsync(Guid id, CancellationToken ct = default) =>
        await MutatePluginAsync($"api/plugins/{id}/enable", HttpMethod.Put, ct);

    public async Task<PluginDto> DisablePluginAsync(Guid id, CancellationToken ct = default) =>
        await MutatePluginAsync($"api/plugins/{id}/disable", HttpMethod.Put, ct);

    public async Task<PluginDto> LoadPluginAsync(Guid id, CancellationToken ct = default) =>
        await MutatePluginAsync($"api/plugins/{id}/load", HttpMethod.Post, ct);

    public async Task<PluginDto> UnloadPluginAsync(Guid id, CancellationToken ct = default) =>
        await MutatePluginAsync($"api/plugins/{id}/unload", HttpMethod.Post, ct);

    public async Task<IReadOnlyList<PluginLogDto>> GetPluginLogsAsync(Guid id, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<PluginLogDto>>(await UrlAsync($"api/plugins/{id}/logs", ct), ct).ConfigureAwait(false)
        ?? [];

    public async Task<PluginPageContentDto?> GetPluginPageContentAsync(string pluginKey, string pageId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<PluginPageContentDto>(
            await UrlAsync($"api/plugins/pages/{Uri.EscapeDataString(pluginKey)}/{Uri.EscapeDataString(pageId)}", ct),
            ct).ConfigureAwait(false);

    public async Task<PluginDeveloperDto?> GetPluginDeveloperAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<PluginDeveloperDto>(await UrlAsync("api/plugins/developer", ct), ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<AutomationDto>> GetAutomationsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<AutomationDto>>(await UrlAsync("api/automations", ct), ct).ConfigureAwait(false)
        ?? [];

    public async Task<AutomationStatsDto> GetAutomationStatsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<AutomationStatsDto>(await UrlAsync("api/automations/stats", ct), ct).ConfigureAwait(false)
        ?? new AutomationStatsDto(0, 0, 0, [], []);

    public async Task<AutomationOptionsDto?> GetAutomationOptionsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<AutomationOptionsDto>(await UrlAsync("api/automations/options", ct), ct).ConfigureAwait(false);

    public async Task<AutomationDeveloperDto?> GetAutomationDeveloperAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<AutomationDeveloperDto>(await UrlAsync("api/automations/developer", ct), ct).ConfigureAwait(false);

    public async Task<AutomationDto> CreateAutomationAsync(CreateAutomationRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(await UrlAsync("api/automations", ct), request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<AutomationDto> UpdateAutomationAsync(Guid id, UpdateAutomationRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(await UrlAsync($"api/automations/{id}", ct), request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<AutomationDto> EnableAutomationAsync(Guid id, CancellationToken ct = default) =>
        await MutateAutomationAsync($"api/automations/{id}/enable", HttpMethod.Put, ct);

    public async Task<AutomationDto> DisableAutomationAsync(Guid id, CancellationToken ct = default) =>
        await MutateAutomationAsync($"api/automations/{id}/disable", HttpMethod.Put, ct);

    public async Task<AutomationRunResultDto> RunAutomationAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(await UrlAsync($"api/automations/{id}/run", ct), null, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationRunResultDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task DeleteAutomationAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync(await UrlAsync($"api/automations/{id}", ct), ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<OnboardingStatusDto> GetOnboardingStatusAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<OnboardingStatusDto>(await UrlAsync("api/onboarding/status", ct), ct).ConfigureAwait(false)
        ?? new OnboardingStatusDto(false, false, 0, false);

    public async Task CompleteOnboardingAsync(CancellationToken ct = default)
    {
        var response = await _http.PostAsync(await UrlAsync("api/onboarding/complete", ct), null, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<SampleImportResultDto> ImportSampleDocumentsAsync(CancellationToken ct = default) =>
        await (await _http.PostAsync(await UrlAsync("api/onboarding/import-samples", ct), null, ct).ConfigureAwait(false))
            .Content.ReadFromJsonAsync<SampleImportResultDto>(cancellationToken: ct).ConfigureAwait(false)
        ?? new SampleImportResultDto(0, 0, []);

    public async Task<AppInfoDto?> GetAppInfoAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<AppInfoDto>(await UrlAsync("api/app/info", ct), ct).ConfigureAwait(false);

    public async Task<PerformanceMetricsDto?> GetPerformanceMetricsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<PerformanceMetricsDto>(await UrlAsync("api/app/metrics", ct), ct).ConfigureAwait(false);

    private async Task<AutomationDto> MutateAutomationAsync(string path, HttpMethod method, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, await UrlAsync(path, ct));
        var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    private async Task<PluginDto> MutatePluginAsync(string path, HttpMethod method, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, await UrlAsync(path, ct));
        var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PluginDto>(cancellationToken: ct).ConfigureAwait(false))!;
    }

    private async Task<Uri> UrlAsync(string path, CancellationToken cancellationToken)
    {
        var baseAddress = _cachedBaseAddress
            ??= await _endpointProvider.GetBaseAddressAsync(cancellationToken).ConfigureAwait(false);
        return new Uri(baseAddress, path);
    }
}
