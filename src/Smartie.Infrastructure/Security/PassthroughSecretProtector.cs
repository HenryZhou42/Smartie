using System.Text;
using Smartie.Application.Abstractions;

namespace Smartie.Infrastructure.Security;

/// <summary>
/// Fallback <see cref="ISecretProtector"/> for non-Windows hosts where DPAPI is
/// unavailable. Base64-encodes only (NOT encryption) so the app remains functional;
/// production non-Windows hosts should supply a real protector (e.g. ASP.NET Core
/// Data Protection or a KMS-backed implementation).
/// </summary>
public sealed class PassthroughSecretProtector : ISecretProtector
{
    public string Protect(string plaintext) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));

    public string? Unprotect(string protectedValue)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
