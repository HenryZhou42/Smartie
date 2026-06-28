namespace Smartie.Application.Abstractions;

/// <summary>
/// Encrypts/decrypts small secrets (e.g. API keys) for storage at rest. The default
/// implementation uses OS-backed protection (Windows DPAPI, current user scope).
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts plaintext and returns a storable (base64) representation.</summary>
    string Protect(string plaintext);

    /// <summary>Reverses <see cref="Protect"/>. Returns null if the value can't be decrypted.</summary>
    string? Unprotect(string protectedValue);
}
