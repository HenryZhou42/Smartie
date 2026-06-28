using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Smartie.Domain.Entities;
using Smartie.Infrastructure.Persistence;

namespace Smartie.Tests;

public sealed class ConversationRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<SmartieDbContext> _options;

    public ConversationRepositoryTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<SmartieDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new SmartieDbContext(_options);
        db.Database.EnsureCreated();
    }

    private SmartieDbContext NewContext() => new(_options);

    private async Task<Guid> SeedUserAsync()
    {
        await using var db = NewContext();
        var user = new User { DisplayName = "Tester" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task CreateAndAddMessages_PersistsOrderedMessages()
    {
        var userId = await SeedUserAsync();
        await using var db = NewContext();
        var repo = new ConversationRepository(db);

        var conversation = await repo.CreateAsync(userId, "First chat");
        await repo.AddMessageAsync(conversation.Id, MessageRole.User, "hello");
        await repo.AddMessageAsync(conversation.Id, MessageRole.Assistant, "hi back");

        var loaded = await repo.FindAsync(conversation.Id);

        Assert.NotNull(loaded);
        Assert.Equal("First chat", loaded!.Title);
        Assert.Collection(loaded.Messages,
            m => Assert.Equal("hello", m.Content),
            m => Assert.Equal("hi back", m.Content));
    }

    [Fact]
    public async Task ListAsync_ReturnsUsersConversationsMostRecentlyUpdatedFirst()
    {
        var userId = await SeedUserAsync();
        await using var db = NewContext();
        var repo = new ConversationRepository(db);

        var older = await repo.CreateAsync(userId, "Older");
        var newer = await repo.CreateAsync(userId, "Newer");
        await repo.AddMessageAsync(newer.Id, MessageRole.User, "bump");

        var list = await repo.ListAsync(userId);

        Assert.Equal(2, list.Count);
        Assert.Equal(newer.Id, list[0].Id);
        Assert.Equal(older.Id, list[1].Id);
    }

    [Fact]
    public async Task ListAsync_PinnedConversationsAppearFirst()
    {
        var userId = await SeedUserAsync();
        await using var db = NewContext();
        var repo = new ConversationRepository(db);

        var older = await repo.CreateAsync(userId, "Older");
        var newer = await repo.CreateAsync(userId, "Newer");
        await repo.AddMessageAsync(newer.Id, MessageRole.User, "bump");
        await repo.SetPinnedAsync(older.Id, isPinned: true);

        var list = await repo.ListAsync(userId);

        Assert.Equal(2, list.Count);
        Assert.Equal(older.Id, list[0].Id);
        Assert.True(list[0].IsPinned);
        Assert.Equal(newer.Id, list[1].Id);
    }

    [Fact]
    public async Task EditUserMessageAndTruncateAsync_RemovesFollowingMessages()
    {
        var userId = await SeedUserAsync();
        await using var db = NewContext();
        var repo = new ConversationRepository(db);

        var conversation = await repo.CreateAsync(userId, "Edit test");
        await repo.AddMessageAsync(conversation.Id, MessageRole.User, "first");
        await repo.AddMessageAsync(conversation.Id, MessageRole.Assistant, "reply one");
        var secondUser = await repo.AddMessageAsync(conversation.Id, MessageRole.User, "second");
        await repo.AddMessageAsync(conversation.Id, MessageRole.Assistant, "reply two");

        var edited = await repo.EditUserMessageAndTruncateAsync(conversation.Id, secondUser.Id, "second edited");

        Assert.NotNull(edited);
        Assert.True(edited!.IsEdited);
        Assert.Equal("second edited", edited.Content);

        var loaded = await repo.FindAsync(conversation.Id);
        Assert.NotNull(loaded);
        Assert.Collection(loaded!.Messages,
            m => Assert.Equal("first", m.Content),
            m => Assert.Equal("reply one", m.Content),
            m => Assert.Equal("second edited", m.Content));
    }

    [Fact]
    public async Task DeleteAsync_RemovesConversationAndMessages()
    {
        var userId = await SeedUserAsync();
        await using var db = NewContext();
        var repo = new ConversationRepository(db);

        var conversation = await repo.CreateAsync(userId, "To delete");
        await repo.AddMessageAsync(conversation.Id, MessageRole.User, "bye");

        var deleted = await repo.DeleteAsync(conversation.Id);

        Assert.True(deleted);
        Assert.Null(await repo.FindAsync(conversation.Id));
        await using var verify = NewContext();
        Assert.Empty(verify.Messages);
    }

    public void Dispose() => _connection.Dispose();
}
