using System.Net;
using System.Net.Http.Json;
using Smartie.Contracts;

namespace Smartie.Tests;

public sealed class ApiEndpointTests : IClassFixture<SmartieApiFactory>
{
    private readonly SmartieApiFactory _factory;

    public ApiEndpointTests(SmartieApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateConversation_ThenListsIt()
    {
        var client = _factory.CreateClient();

        var created = await (await client.PostAsJsonAsync(
                "/api/conversations",
                new CreateConversationRequest("My chat")))
            .Content.ReadFromJsonAsync<ConversationDto>();

        Assert.NotNull(created);
        Assert.Equal("My chat", created!.Title);

        var list = await client.GetFromJsonAsync<List<ConversationDto>>("/api/conversations");
        Assert.Contains(list!, c => c.Id == created.Id);
    }

    [Fact]
    public async Task SendMessage_PersistsUserAndAssistantTurns()
    {
        var client = _factory.CreateClient();
        var conversation = await (await client.PostAsJsonAsync(
                "/api/conversations",
                new CreateConversationRequest()))
            .Content.ReadFromJsonAsync<ConversationDto>();

        var reply = await (await client.PostAsJsonAsync(
                $"/api/conversations/{conversation!.Id}/messages",
                new SendMessageRequest("hello there")))
            .Content.ReadFromJsonAsync<MessageDto>();

        Assert.NotNull(reply);
        Assert.Equal("assistant", reply!.Role);
        Assert.Equal("Hello from Smartie", reply.Content);

        var messages = await client.GetFromJsonAsync<List<MessageDto>>(
            $"/api/conversations/{conversation.Id}/messages");

        Assert.Collection(messages!,
            m => Assert.Equal("user", m.Role),
            m => Assert.Equal("assistant", m.Role));
    }

    [Fact]
    public async Task SendMessage_EmptyContent_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var conversation = await (await client.PostAsJsonAsync(
                "/api/conversations",
                new CreateConversationRequest()))
            .Content.ReadFromJsonAsync<ConversationDto>();

        var response = await client.PostAsJsonAsync(
            $"/api/conversations/{conversation!.Id}/messages",
            new SendMessageRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StreamMessage_EmitsSseDeltas()
    {
        var client = _factory.CreateClient();
        var conversation = await (await client.PostAsJsonAsync(
                "/api/conversations",
                new CreateConversationRequest()))
            .Content.ReadFromJsonAsync<ConversationDto>();

        var response = await client.PostAsJsonAsync(
            $"/api/conversations/{conversation!.Id}/messages/stream",
            new SendMessageRequest("stream please"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("data: \"Hello \"", body);
        Assert.Contains("data: \"from \"", body);
        Assert.Contains("data: \"Smartie\"", body);
    }

    [Fact]
    public async Task CreateTask_ThenListsAndCompletesIt()
    {
        var client = _factory.CreateClient();

        var created = await (await client.PostAsJsonAsync(
                "/api/tasks",
                new CreateTaskRequest("Build Smartie RAG", "Implement semantic retrieval.", "High")))
            .Content.ReadFromJsonAsync<TaskDto>();

        Assert.NotNull(created);
        Assert.Equal("Build Smartie RAG", created!.Title);
        Assert.Equal("High", created.Priority);

        var list = await client.GetFromJsonAsync<List<TaskDto>>("/api/tasks");
        Assert.Contains(list!, t => t.Id == created.Id);

        var completed = await (await client.PutAsync($"/api/tasks/{created.Id}/complete", null))
            .Content.ReadFromJsonAsync<TaskDto>();

        Assert.NotNull(completed);
        Assert.Equal("Completed", completed!.Status);
    }

    [Fact]
    public async Task RecordRecentFile_ThenListsAndPinsIt()
    {
        var client = _factory.CreateClient();
        var tempDir = Path.Combine(Path.GetTempPath(), "smartie-api-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "Smartie_Api.txt");
        await File.WriteAllTextAsync(filePath, "api test");

        try
        {
            var recorded = await (await client.PostAsJsonAsync(
                    "/api/files/recent",
                    new RecordRecentFileRequest(filePath)))
                .Content.ReadFromJsonAsync<RecentFileDto>();

            Assert.NotNull(recorded);
            Assert.Equal("Smartie_Api.txt", recorded!.FileName);

            var list = await client.GetFromJsonAsync<List<RecentFileDto>>("/api/files/recent");
            Assert.Contains(list!, f => f.Id == recorded.Id);

            var pinned = await (await client.PutAsJsonAsync(
                    $"/api/files/recent/{recorded.Id}/pin",
                    new PinRecentFileRequest(true)))
                .Content.ReadFromJsonAsync<RecentFileDto>();

            Assert.NotNull(pinned);
            Assert.True(pinned!.Pinned);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SearchFiles_ReturnsMatchesInFavoriteFolder()
    {
        var client = _factory.CreateClient();
        var tempDir = Path.Combine(Path.GetTempPath(), "smartie-api-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "Smartie_Search_Api.txt"), "search me");

        try
        {
            var favoriteResponse = await client.PostAsJsonAsync(
                "/api/files/favorites",
                new AddFavoriteFolderRequest(tempDir, "API Search"));
            favoriteResponse.EnsureSuccessStatusCode();

            var response = await (await client.PostAsJsonAsync(
                    "/api/files/search",
                    new FileSearchRequest("smartie")))
                .Content.ReadFromJsonAsync<FileSearchResponseDto>();

            Assert.NotNull(response);
            Assert.Contains(response!.Results, r => r.FileName.Contains("Smartie_Search_Api", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateAppearanceSettings_PersistsThemeAndAccent()
    {
        var client = _factory.CreateClient();

        var updated = await (await client.PutAsJsonAsync(
                "/api/appearance/settings",
                new UpdateAppearanceSettingsRequest(
                    "Midnight", "Green", null, null, "Large", "Compact", "ReducedMotion", true, "Blur",
                    null, null, "Large", "Wide", "Relaxed", "Monokai", "GitHub")))
            .Content.ReadFromJsonAsync<AppearanceSettingsDto>();

        Assert.NotNull(updated);
        Assert.Equal("Midnight", updated!.Theme);
        Assert.Equal("Green", updated.AccentColor);
        Assert.Equal("Compact", updated.SidebarMode);
        Assert.Equal("ReducedMotion", updated.AnimationMode);

        var loaded = await client.GetFromJsonAsync<AppearanceSettingsDto>("/api/appearance/settings");
        Assert.NotNull(loaded);
        Assert.Equal("Midnight", loaded!.Theme);
        Assert.Equal("Large", loaded.FontSize);
    }

    [Fact]
    public async Task ScanPlugins_ReturnsDeveloperStats()
    {
        var client = _factory.CreateClient();

        var scan = await (await client.PostAsync("/api/plugins/scan", null))
            .Content.ReadFromJsonAsync<PluginScanResultDto>();

        Assert.NotNull(scan);
        var developer = await client.GetFromJsonAsync<PluginDeveloperDto>("/api/plugins/developer");
        Assert.NotNull(developer);
    }

    [Fact]
    public async Task CreateAutomation_ThenListsIt()
    {
        var client = _factory.CreateClient();

        var created = await (await client.PostAsJsonAsync(
                "/api/automations",
                new CreateAutomationRequest(
                    "Test Workflow",
                    "Local automation",
                    "Manual",
                    "CreateTask",
                    "{\"action\":{\"title\":\"Follow up\"}}",
                    true)))
            .Content.ReadFromJsonAsync<AutomationDto>();

        Assert.NotNull(created);
        Assert.Equal("Test Workflow", created!.Name);

        var list = await client.GetFromJsonAsync<List<AutomationDto>>("/api/automations");
        Assert.Contains(list!, a => a.Id == created.Id);

        var stats = await client.GetFromJsonAsync<AutomationStatsDto>("/api/automations/stats");
        Assert.NotNull(stats);
        Assert.True(stats!.TotalCount >= 1);
    }
}
