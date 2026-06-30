using Smartie.Application.Abstractions;
using Smartie.Contracts;

namespace Smartie.Api.Endpoints;

public static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/onboarding");

        group.MapGet("/status", async (IOnboardingService onboarding, ICurrentUser user, CancellationToken ct) =>
        {
            var status = await onboarding.GetStatusAsync(user.UserId, ct);
            return Results.Ok(new OnboardingStatusDto(
                status.HasCompletedOnboarding,
                status.HasConfiguredProvider,
                status.DocumentCount,
                status.SampleDocumentsAvailable));
        });

        group.MapPost("/complete", async (IOnboardingService onboarding, ICurrentUser user, CancellationToken ct) =>
        {
            await onboarding.CompleteAsync(user.UserId, ct);
            return Results.NoContent();
        });

        group.MapPost("/import-samples", async (IOnboardingService onboarding, ICurrentUser user, CancellationToken ct) =>
        {
            var result = await onboarding.ImportSampleDocumentsAsync(user.UserId, ct);
            return Results.Ok(new SampleImportResultDto(result.ImportedCount, result.SkippedCount, result.ImportedNames));
        });

        return app;
    }
}
