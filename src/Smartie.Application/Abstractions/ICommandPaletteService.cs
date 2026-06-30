namespace Smartie.Application.Abstractions;

public interface ICommandPaletteService
{
    Task<CommandSearchResponse> SearchAsync(
        Guid userId,
        string? query,
        CancellationToken cancellationToken = default);

    Task RecordUsageAsync(
        Guid userId,
        string commandId,
        CancellationToken cancellationToken = default);

    Task<CommandPaletteDeveloperStats> GetDeveloperStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
