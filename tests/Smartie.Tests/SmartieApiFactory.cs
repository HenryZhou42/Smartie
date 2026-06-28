using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Smartie.Application.Abstractions;

namespace Smartie.Tests;

/// <summary>
/// Boots the real API with a throwaway SQLite file and a fake AI provider so the
/// HTTP surface can be exercised without calling a real model.
/// </summary>
public sealed class SmartieApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"smartie-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Smartie", $"Data Source={_dbPath}");

        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IChatAiService));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IChatAiService>(_ => new FakeChatAiService("Hello ", "from ", "Smartie"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // Best-effort cleanup of the temp database.
            }
        }
    }
}
