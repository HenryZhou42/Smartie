using Smartie.Application.Abstractions;
using Smartie.Infrastructure.Persistence;

namespace Smartie.Api;

/// <summary>
/// Stand-in <see cref="ICurrentUser"/> for Community Edition — always resolves to
/// the seeded local profile. A future edition can replace this implementation.
/// </summary>
public sealed class LocalCurrentUser : ICurrentUser
{
    public Guid UserId => DbInitializer.LocalUserId;
}
