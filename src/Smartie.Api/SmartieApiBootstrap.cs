using Smartie.Api.Endpoints;
using Smartie.Application.Abstractions;
using Smartie.Application.DependencyInjection;
using Smartie.Infrastructure.DependencyInjection;
using Smartie.Infrastructure.Persistence;

namespace Smartie.Api;

/// <summary>
/// Shared wiring for the local Smartie HTTP API (standalone host or embedded in MAUI).
/// </summary>
public static class SmartieApiBootstrap
{
    public const string ClientCorsPolicy = "SmartieClient";

    public static WebApplicationBuilder AddSmartieApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
        builder.Services.AddSmartieApplication();
        builder.Services.AddSmartieInfrastructure(builder.Configuration);
        builder.Services.AddSingleton<ICurrentUser, LocalCurrentUser>();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(ClientCorsPolicy, policy =>
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        });

        return builder;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartieDbContext>();
        await DbInitializer.InitializeAsync(db, cancellationToken);
    }

    public static WebApplication MapSmartieApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseCors(ClientCorsPolicy);

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapConversationEndpoints();
        app.MapSettingsEndpoints();
        app.MapDocumentEndpoints();

        return app;
    }
}
