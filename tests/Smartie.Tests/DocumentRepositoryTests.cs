using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Smartie.Domain.Entities;
using Smartie.Infrastructure.Persistence;

namespace Smartie.Tests;

public sealed class DocumentRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<SmartieDbContext> _options;

    public DocumentRepositoryTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<SmartieDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new SmartieDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task AddAndSearchDocuments_PersistsMetadata()
    {
        var userId = await SeedUserAsync();
        await using var db = new SmartieDbContext(_options);
        var repo = new DocumentRepository(db);

        await repo.AddAsync(new Document
        {
            UserId = userId,
            Name = "Employee Contract",
            FileName = "EmployeeContract.pdf",
            Extension = "pdf",
            RelativePath = $"{Guid.NewGuid():N}/EmployeeContract.pdf",
            SizeBytes = 3_250_000
        });

        var results = await repo.ListAsync(userId, "Employee");
        var doc = Assert.Single(results);

        Assert.Equal("Employee Contract", doc.Name);
        Assert.Equal("pdf", doc.Extension);
        Assert.False(doc.IsIndexed);
    }

    [Fact]
    public async Task RenameAndDeleteDocument_UpdatesAndRemovesRow()
    {
        var userId = await SeedUserAsync();
        await using var db = new SmartieDbContext(_options);
        var repo = new DocumentRepository(db);

        var created = await repo.AddAsync(new Document
        {
            UserId = userId,
            Name = "Notes",
            FileName = "notes.txt",
            Extension = "txt",
            RelativePath = $"{Guid.NewGuid():N}/notes.txt",
            SizeBytes = 120
        });

        var renamed = await repo.UpdateNameAsync(created.Id, userId, "Meeting Notes");
        Assert.NotNull(renamed);
        Assert.Equal("Meeting Notes", renamed!.Name);

        var deleted = await repo.DeleteAsync(created.Id, userId);
        Assert.True(deleted);
        Assert.Empty(await repo.ListAsync(userId, null));
    }

    private async Task<Guid> SeedUserAsync()
    {
        await using var db = new SmartieDbContext(_options);
        var user = new User { DisplayName = "Tester" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }
}
