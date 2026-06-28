namespace Smartie.Shared.Services;

/// <summary>Uses a fixed API base URL (Web host or external MAUI API).</summary>
public sealed class FixedSmartieApiEndpointProvider : ISmartieApiEndpointProvider
{
    private readonly Uri _baseAddress;

    public FixedSmartieApiEndpointProvider(Uri baseAddress)
    {
        _baseAddress = baseAddress;
    }

    public Task<Uri> GetBaseAddressAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_baseAddress);
}
