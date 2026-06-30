using Microsoft.EntityFrameworkCore;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Persistence;

public sealed class RecentCommandRepository : IRecentCommandRepository
{
    private readonly SmartieDbContext _db;

    public RecentCommandRepository(SmartieDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RecentCommand>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _db.RecentCommands
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.LastUsed)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<RecentCommand?> FindByNameAsync(
        Guid userId,
        string commandName,
        CancellationToken cancellationToken = default) =>
        _db.RecentCommands
            .FirstOrDefaultAsync(
                c => c.UserId == userId && c.CommandName == commandName,
                cancellationToken);

    public async Task RecordUsageAsync(
        Guid userId,
        string commandName,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.RecentCommands
            .FirstOrDefaultAsync(
                c => c.UserId == userId && c.CommandName == commandName,
                cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            _db.RecentCommands.Add(new RecentCommand
            {
                UserId = userId,
                CommandName = commandName,
                UsageCount = 1,
                LastUsed = now
            });
        }
        else
        {
            existing.UsageCount++;
            existing.LastUsed = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<int> CountForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _db.RecentCommands.AsNoTracking().CountAsync(c => c.UserId == userId, cancellationToken);
}
