using Microsoft.Extensions.Logging.Abstractions;
using Smartie.Infrastructure.Security;

namespace Smartie.Tests;

public class SecretProtectionTests
{
    [Fact]
    public void WindowsSecret_RoundTripsAndRejectsCorruptBlob()
    {
        if (!OperatingSystem.IsWindows()) return;
        var protector = new DpapiSecretProtector(NullLogger<DpapiSecretProtector>.Instance);
        const string key = "synthetic-test-secret";
        var encrypted = protector.Protect(key);
        Assert.DoesNotContain(key, encrypted);
        Assert.Equal(key, protector.Unprotect(encrypted));
        Assert.Null(protector.Unprotect("corrupt-blob"));
    }
}
