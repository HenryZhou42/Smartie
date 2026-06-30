using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IRecentCommandRepository
{
    Task<IReadOnlyList<RecentCommand>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RecentCommand?> FindByNameAsync(
        Guid userId,
        string commandName,
        CancellationToken cancellationToken = default);

    Task RecordUsageAsync(
        Guid userId,
        string commandName,
        CancellationToken cancellationToken = default);

    Task<int> CountForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
