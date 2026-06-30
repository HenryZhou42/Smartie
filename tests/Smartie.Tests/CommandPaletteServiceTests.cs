using Smartie.Application.Abstractions;
using Smartie.Application.Services;
using Smartie.Domain.Entities;

namespace Smartie.Tests;

public class CommandPaletteServiceTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000201");

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsStaticCommands()
    {
        var service = CreateService(out _);
        var response = await service.SearchAsync(UserId, query: null);

        Assert.NotEmpty(response.Results);
        Assert.Contains(response.Results, r => r.Id == "chat.new");
        Assert.True(response.Developer.SearchLatencyMs >= 0);
    }

    [Fact]
    public async Task SearchAsync_KbQuery_MatchesKnowledgeBase()
    {
        var service = CreateService(out _);
        var response = await service.SearchAsync(UserId, "kb");

        Assert.NotEmpty(response.Results);
        Assert.Contains(response.Results, r => r.Title.Contains("Knowledge Base", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecordUsageAsync_RanksRecentlyUsedHigher()
    {
        var service = CreateService(out var recentCommands);
        await service.RecordUsageAsync(UserId, "chat.new");
        await service.RecordUsageAsync(UserId, "chat.new");
        await service.RecordUsageAsync(UserId, "nav.dashboard");

        var response = await service.SearchAsync(UserId, query: null);
        var chat = response.Results.First(r => r.Id == "chat.new");
        var dashboard = response.Results.First(r => r.Id == "nav.dashboard");

        Assert.Equal(2, chat.UsageCount);
        Assert.True(chat.RankingScore >= dashboard.RankingScore);
    }

    [Fact]
    public async Task SearchAsync_IncludesRecentConversations()
    {
        var repository = new InMemoryConversationRepository();
        await repository.CreateAsync(UserId, "Smartie planning");
        var service = new CommandPaletteService(
            new InMemoryRecentCommandRepository(),
            repository,
            new Smartie.Infrastructure.Plugins.PluginRegistry());

        var response = await service.SearchAsync(UserId, "smartie");
        Assert.Contains(response.Results, r => r.Title == "Smartie planning");
    }

    private static CommandPaletteService CreateService(
        out InMemoryRecentCommandRepository recentCommands,
        IConversationRepository? conversations = null)
    {
        recentCommands = new InMemoryRecentCommandRepository();
        conversations ??= new InMemoryConversationRepository();
        return new CommandPaletteService(recentCommands, conversations, new Smartie.Infrastructure.Plugins.PluginRegistry());
    }
}

internal sealed class InMemoryRecentCommandRepository : IRecentCommandRepository
{
    private readonly Dictionary<(Guid UserId, string Name), RecentCommand> _commands = new();

    public Task<IReadOnlyList<RecentCommand>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RecentCommand>>(
            _commands.Values.Where(c => c.UserId == userId).OrderByDescending(c => c.LastUsed).ToList());

    public Task<RecentCommand?> FindByNameAsync(
        Guid userId,
        string commandName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_commands.TryGetValue((userId, commandName), out var command) ? command : null);

    public Task RecordUsageAsync(
        Guid userId,
        string commandName,
        CancellationToken cancellationToken = default)
    {
        if (!_commands.TryGetValue((userId, commandName), out var command))
        {
            command = new RecentCommand
            {
                UserId = userId,
                CommandName = commandName,
                UsageCount = 0,
                LastUsed = DateTimeOffset.UtcNow
            };
            _commands[(userId, commandName)] = command;
        }

        command.UsageCount++;
        command.LastUsed = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<int> CountForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_commands.Values.Count(c => c.UserId == userId));
}
