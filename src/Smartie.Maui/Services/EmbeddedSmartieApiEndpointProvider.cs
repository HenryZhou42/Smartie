using Smartie.Maui.Hosting;
using Smartie.Shared.Services;

namespace Smartie.Maui.Services;

/// <summary>Starts the embedded local API and returns its loopback base URL.</summary>
public sealed class EmbeddedSmartieApiEndpointProvider : ISmartieApiEndpointProvider
{
    private readonly LocalSmartieApiHost _host;

    public EmbeddedSmartieApiEndpointProvider(LocalSmartieApiHost host)
    {
        _host = host;
    }

    public async Task<Uri> GetBaseAddressAsync(CancellationToken cancellationToken = default)
    {
        await _host.EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        return new Uri(_host.BaseUrl.TrimEnd('/') + "/");
    }
}
