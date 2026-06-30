using Microsoft.EntityFrameworkCore;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Persistence;

public sealed class AppearanceRepository : IAppearanceRepository
{
    private readonly SmartieDbContext _db;

    public AppearanceRepository(SmartieDbContext db)
    {
        _db = db;
    }

    public Task<UserPreferences?> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public Task<UserPreferences?> GetForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public async Task<UserPreferences> AddAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        _db.UserPreferences.Add(preferences);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return preferences;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
