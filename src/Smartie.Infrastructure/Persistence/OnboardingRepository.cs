using Microsoft.EntityFrameworkCore;
using Smartie.Application.Abstractions;

namespace Smartie.Infrastructure.Persistence;

public sealed class OnboardingRepository : IOnboardingRepository
{
    private readonly SmartieDbContext _db;

    public OnboardingRepository(SmartieDbContext db)
    {
        _db = db;
    }

    public async Task<bool> GetHasCompletedOnboardingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var value = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.HasCompletedOnboarding)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return value;
    }

    public async Task SetCompletedOnboardingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        user.HasCompletedOnboarding = true;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkDocumentAsSampleAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return;
        }

        document.IsSample = true;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
