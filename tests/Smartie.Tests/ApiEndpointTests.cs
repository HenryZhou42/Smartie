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
}
