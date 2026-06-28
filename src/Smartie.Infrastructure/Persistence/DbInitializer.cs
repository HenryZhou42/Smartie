using Microsoft.EntityFrameworkCore;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Persistence;

/// <summary>
/// Applies migrations and seeds the single local profile used by Community Edition.
/// </summary>
public static class DbInitializer
{
    public static readonly Guid LocalUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static async Task InitializeAsync(SmartieDbContext db, CancellationToken cancellationToken = default)
    {
        await ReleaseStaleMigrationLockAsync(db, cancellationToken).ConfigureAwait(false);
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        var exists = await db.Users
            .AnyAsync(u => u.Id == LocalUserId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            db.Users.Add(new User { Id = LocalUserId, DisplayName = "Local User" });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Clears orphaned EF migration locks left behind when a previous Smartie process crashed
    /// mid-migration. Without this, startup can hang forever waiting for the lock.
    /// </summary>
    private static async Task ReleaseStaleMigrationLockAsync(
        SmartieDbContext db,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlite())
        {
            return;
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM __EFMigrationsLock WHERE Id = 1",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Lock table may not exist yet on first run.
        }
    }
}
