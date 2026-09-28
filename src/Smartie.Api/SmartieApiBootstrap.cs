using Smartie.Api.Endpoints;
using Smartie.Application.Abstractions;
using Smartie.Application.DependencyInjection;
using Smartie.Contracts;
using Smartie.Infrastructure.DependencyInjection;
using Smartie.Infrastructure.Persistence;
using Smartie.Infrastructure.Storage;

namespace Smartie.Api;

/// <summary>
/// Shared wiring for the local Smartie HTTP API (standalone host or embedded in MAUI).
/// </summary>
public static class SmartieApiBootstrap
{
    public const string ClientCorsPolicy = "SmartieClient";
    // MAUI BlazorWebView origins and the repository's browser development profiles.
    private static readonly string[] ClientOrigins =
        ["https://0.0.0.1", "http://0.0.0.1", "http://localhost:5072", "https://localhost:7060"];

    public static WebApplicationBuilder AddSmartieApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
        builder.Services.AddSmartieApplication();
        builder.Services.AddSmartieInfrastructure(builder.Configuration);
        builder.Services.AddSingleton<ICurrentUser, LocalCurrentUser>();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(ClientCorsPolicy, policy =>
                policy.WithOrigins(ClientOrigins).AllowAnyHeader().AllowAnyMethod());
        });

        return builder;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartieDbContext>();
        SmartiePaths.EnsureAppDataLayout();
        await DbInitializer.InitializeAsync(db, cancellationToken);
    }

    public static WebApplication MapSmartieApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        // CORS alone does not reject simple cross-origin POST requests.
        app.Use(async (context, next) =>
        {
            var origin = context.Request.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(origin) && !ClientOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next(context);
        });
        app.UseCors(ClientCorsPolicy);

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapConversationEndpoints();
        app.MapSettingsEndpoints();
        app.MapDocumentEndpoints();
        app.MapMemoryEndpoints();
        app.MapCommandPaletteEndpoints();
        app.MapTaskEndpoints();
        app.MapFileIntegrationEndpoints();
        app.MapAppearanceEndpoints();
        app.MapPluginEndpoints();
        app.MapAutomationEndpoints();
        app.MapOnboardingEndpoints();
        app.MapGet("/api/app/info", () => Results.Ok(new AppInfoDto(
            ProductMetadata.ProductName,
            ProductMetadata.Edition,
            ProductMetadata.Version,
            ProductMetadata.BuildNumber,
            ProductMetadata.BuildNumber,
            ProductMetadata.ReleaseLabel,
            ProductMetadata.Description,
            ProductMetadata.GitHubUrl,
            ProductMetadata.License)));

        app.MapGet("/api/app/metrics", (IAppMetricsService metrics) =>
        {
            var snapshot = metrics.GetMetrics();
            return Results.Ok(new PerformanceMetricsDto(
                snapshot.StartupTimeMs,
                snapshot.LastSearchLatencyMs,
                snapshot.LastRagLatencyMs));
        });

        return app;
    }
}
