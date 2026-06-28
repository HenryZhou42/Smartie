namespace Smartie.Shared.Services;

/// <summary>
/// Resolves the Smartie API base URL before HTTP requests are sent.
/// </summary>
public interface ISmartieApiEndpointProvider
{
    Task<Uri> GetBaseAddressAsync(CancellationToken cancellationToken = default);
}
