namespace Smartie.Application.Abstractions;

/// <summary>
/// Provides the current user's id. Community Edition always resolves to the
/// seeded local profile; a future edition can replace this with an authenticated principal.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
}
