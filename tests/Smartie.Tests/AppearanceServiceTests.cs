using Smartie.Application.Abstractions;
using Smartie.Application.Services;
using Smartie.Contracts;
using Smartie.Domain.Entities;

namespace Smartie.Tests;

public class AppearanceServiceTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000501");

    [Fact]
    public async Task GetSettingsAsync_CreatesDefaultPreferences()
    {
        var repository = new InMemoryAppearanceRepository(UserId);
        var service = new AppearanceService(repository);

        var settings = await service.GetSettingsAsync(UserId);

        Assert.Equal("Default", settings.Theme);
        Assert.Equal("Purple", settings.AccentColor);
        Assert.Equal("Expanded", settings.SidebarMode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ChangesThemeAndAccent()
    {
        var repository = new InMemoryAppearanceRepository(UserId);
        var service = new AppearanceService(repository);

        await service.UpdateSettingsAsync(
            UserId,
            new AppearanceSettingsUpdate("Light", "Blue", null, null, null, "Compact", null, null, null, null, null, null, null, null, null, null));

        var settings = await service.GetSettingsAsync(UserId);

        Assert.Equal("Light", settings.Theme);
        Assert.Equal("Blue", settings.AccentColor);
        Assert.Equal("Compact", settings.SidebarMode);
    }

    [Fact]
    public async Task GetDeveloperSnapshotAsync_ReturnsCssVariables()
    {
        var repository = new InMemoryAppearanceRepository(UserId);
        var service = new AppearanceService(repository);

        var snapshot = await service.GetDeveloperSnapshotAsync(UserId);

        Assert.Contains(snapshot.Variables, v => v.Name == "--smartie-accent");
        Assert.Contains(snapshot.Variables, v => v.Name == "data-smartie-theme");
    }
}

public class AppearanceThemeMapperTests
{
    [Fact]
    public void Map_ResolvesCustomAccent()
    {
        var settings = new AppearanceSettingsDto(
            "Default", "Custom", "#ff8800", "Default", "Medium", "Expanded", "Enabled", false, "Disabled",
            20, 200, "Medium", "Standard", "Normal", "Default", "Default", DateTimeOffset.UtcNow);

        var mapped = AppearanceThemeMapper.Map(settings);

        Assert.Contains(mapped, v => v.Name == "--smartie-accent" && v.Value == "#ff8800");
    }

    [Fact]
    public void ResolveSidebarAttribute_MapsIconsOnly()
    {
        Assert.Equal("icons-only", AppearanceThemeMapper.ResolveSidebarAttribute("IconsOnly"));
    }
}

internal sealed class InMemoryAppearanceRepository : IAppearanceRepository
{
    private UserPreferences? _preferences;
    private readonly Guid _userId;

    public InMemoryAppearanceRepository(Guid userId) => _userId = userId;

    public Task<UserPreferences?> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(userId == _userId ? _preferences : null);

    public Task<UserPreferences?> GetForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        GetForUserAsync(userId, cancellationToken);

    public Task<UserPreferences> AddAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        _preferences = preferences;
        return Task.FromResult(preferences);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
