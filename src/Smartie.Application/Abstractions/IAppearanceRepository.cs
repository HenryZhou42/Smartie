using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IAppearanceRepository
{
    Task<UserPreferences?> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserPreferences?> GetForUpdateAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserPreferences> AddAsync(UserPreferences preferences, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
