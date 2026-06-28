using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Smartie.Application.Abstractions;

namespace Smartie.Infrastructure.Security;

/// <summary>
/// <see cref="ISecretProtector"/> backed by Windows DPAPI (current-user scope), so
/// stored API keys are encrypted at rest and only decryptable by the same Windows
/// user on the same machine.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProtector : ISecretProtector
{
    // Extra entropy mixed into the DPAPI blob; not secret, just app-specific.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Smartie.AiProviderCredential.v1");

    private readonly ILogger<DpapiSecretProtector> _logger;

    public DpapiSecretProtector(ILogger<DpapiSecretProtector> logger)
    {
        _logger = logger;
    }

    public string Protect(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public string? Unprotect(string protectedValue)
    {
        try
        {
            var encrypted = Convert.FromBase64String(protectedValue);
            var bytes = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            // Blob was tampered with, corrupted, or created by a different user/machine.
            _logger.LogWarning(ex, "Failed to decrypt a stored secret; treating it as unset.");
            return null;
        }
    }
}
