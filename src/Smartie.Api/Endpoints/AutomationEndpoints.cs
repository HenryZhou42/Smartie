using Smartie.Application.Abstractions;
using Smartie.Contracts;
using Smartie.Domain.Entities;

namespace Smartie.Api.Endpoints;

public static class AutomationEndpoints
{
    public static IEndpointRouteBuilder MapAutomationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/automations");

        group.MapGet("/", async (IAutomationService automations, ICurrentUser user, CancellationToken ct) =>
        {
            var results = await automations.ListAsync(user.UserId, ct);
            return Results.Ok(results.Select(ToDto));
        });

        group.MapGet("/stats", async (IAutomationService automations, ICurrentUser user, CancellationToken ct) =>
        {
            var stats = await automations.GetStatsAsync(user.UserId, ct);
            return Results.Ok(new AutomationStatsDto(
                stats.TotalCount,
                stats.EnabledCount,
                stats.DisabledCount,
                stats.UpcomingRuns.Select(ToDto).ToList(),
                stats.RecentRuns.Select(ToLogDto).ToList()));
        });

        group.MapGet("/options", () => Results.Ok(new AutomationOptionsDto(
            Enum.GetNames<AutomationTriggerType>(),
            Enum.GetNames<AutomationActionType>(),
            Enum.GetNames<AutomationConditionType>(),
            ["Daily", "Weekly"])));

        group.MapGet("/developer", async (IAutomationService automations, ICurrentUser user, CancellationToken ct) =>
        {
            var stats = await automations.GetDeveloperStatsAsync(user.UserId, ct);
            return Results.Ok(new AutomationDeveloperDto(
                stats.AutomationCount,
                stats.EnabledCount,
                stats.TotalExecutionTimeMs,
                stats.FailureCount,
                stats.SuccessCount,
                stats.SuccessRatePercent,
                stats.RecentLogs.Select(ToLogDto).ToList()));
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            IAutomationService automations,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var automation = await automations.GetAsync(user.UserId, id, ct);
            return automation is null ? Results.NotFound() : Results.Ok(ToDto(automation));
        });

        group.MapGet("/{id:guid}/logs", async (
            Guid id,
            int? limit,
            IAutomationService automations,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var logs = await automations.ListRunLogsAsync(user.UserId, id, limit ?? 50, ct);
            return Results.Ok(logs.Select(ToLogDto));
        });

        group.MapPost("/", async (
            Contracts.CreateAutomationRequest request,
            IAutomationService automations,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest("Name must not be empty.");
            }

            var created = await automations.CreateAsync(
                user.UserId,
                new Smartie.Application.Abstractions.CreateAutomationRequest(
                    request.Name,
                    request.Description,
                    request.TriggerType,
                    request.ActionType,
                    request.ConfigJson,
                    request.Enabled),
                ct);
            return Results.Ok(ToDto(created));
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            Contracts.UpdateAutomationRequest request,
            IAutomationService automations,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest("Name must not be empty.");
            }

            var updated = await automations.UpdateAsync(
                user.UserId,
                id,
                new Smartie.Application.Abstractions.UpdateAutomationRequest(
                    request.Name,
                    request.Description,
                    request.TriggerType,
                    request.ActionType,
                    request.ConfigJson,
                    request.Enabled),
                ct);
            return updated is null ? Results.NotFound() : Results.Ok(ToDto(updated));
        });

        group.MapPut("/{id:guid}/enable", async (
            Guid id,
            IAutomationService automations,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var updated = await automations.EnableAsync(user.UserId, id, ct);
            return updated is null ? Results.NotFound() : Results.Ok(ToDto(updated));
        });

        group.MapPut("/{id:guid}/disable", async (
            Guid id,
            IAutomationService automations,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var updated = await automations.DisableAsync(user.UserId, id, ct);
            return updated is null ? Results.NotFound() : Results.Ok(ToDto(updated));
        });

        group.MapPost("/{id:guid}/run", async (
            Guid id,
            IAutomationService automations,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            try
            {
                var result = await automations.RunNowAsync(user.UserId, id, null, ct);
                return Results.Ok(new AutomationRunResultDto(
                    result.AutomationId,
                    result.Status.ToString(),
                    result.Message,
                    result.DurationMs));
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(ex.Message);
            }
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            IAutomationService automations,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var deleted = await automations.DeleteAsync(user.UserId, id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }

    private static AutomationDto ToDto(AutomationSnapshot automation) =>
        new(
            automation.Id,
            automation.Name,
            automation.Description,
            automation.Enabled,
            automation.TriggerType.ToString(),
            automation.ActionType.ToString(),
            automation.ConfigJson,
            automation.CreatedAt,
            automation.UpdatedAt,
            automation.LastRun,
            automation.NextRun,
            automation.RunCount);

    private static AutomationRunLogDto ToLogDto(AutomationRunLogSnapshot log) =>
        new(
            log.Id,
            log.AutomationRuleId,
            log.AutomationName,
            log.Status.ToString(),
            log.Message,
            log.DurationMs,
            log.StartedAt,
            log.CompletedAt);
}