using Microsoft.EntityFrameworkCore;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Persistence;

public sealed class PluginRepository : IPluginRepository
{
    private readonly SmartieDbContext _db;

    public PluginRepository(SmartieDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PluginInstallation>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _db.PluginInstallations
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<PluginInstallation?> FindAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default) =>
        _db.PluginInstallations
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == id, cancellationToken);

    public Task<PluginInstallation?> FindForUpdateAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default) =>
        _db.PluginInstallations
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == id, cancellationToken);

    public Task<PluginInstallation?> FindByKeyAsync(
        Guid userId,
        string pluginKey,
        CancellationToken cancellationToken = default) =>
        _db.PluginInstallations
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PluginKey == pluginKey, cancellationToken);

    public Task<PluginInstallation?> FindByKeyForUpdateAsync(
        Guid userId,
        string pluginKey,
        CancellationToken cancellationToken = default) =>
        _db.PluginInstallations
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PluginKey == pluginKey, cancellationToken);

    public async Task<PluginInstallation> AddAsync(
        PluginInstallation installation,
        CancellationToken cancellationToken = default)
    {
        _db.PluginInstallations.Add(installation);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return installation;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<PluginLogEntry>> ListLogsAsync(
        Guid userId,
        Guid pluginId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var installation = await FindAsync(userId, pluginId, cancellationToken).ConfigureAwait(false);
        if (installation is null)
        {
            return Array.Empty<PluginLogEntry>();
        }

        return await _db.PluginLogEntries
            .AsNoTracking()
            .Where(l => l.PluginInstallationId == pluginId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddLogAsync(PluginLogEntry entry, CancellationToken cancellationToken = default)
    {
        _db.PluginLogEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.PluginInstallations.AsNoTracking().CountAsync(p => p.UserId == userId, cancellationToken);
}
