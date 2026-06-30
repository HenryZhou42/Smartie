using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Application.Services;
using Smartie.Domain.Entities;

namespace Smartie.Tests;

public class MemoryPromptBuilderTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000101");

    [Fact]
    public async Task BuildMemoryContextAsync_WhenDisabled_ReturnsNullBlock()
    {
        var memory = new FakeMemoryService { Enabled = false };
        var builder = CreateBuilder(memory);

        var (block, diagnostics) = await builder.BuildMemoryContextAsync(UserId, "hello");

        Assert.Null(block);
        Assert.Equal(0, diagnostics.RetrievedCount);
    }

    [Fact]
    public async Task BuildMemoryContextAsync_IncludesPinnedAndSearchedMemories()
    {
        var memory = new FakeMemoryService
        {
            Pinned =
            [
                new Memory
                {
                    Id = Guid.NewGuid(),
                    Content = "Prefers Gemini",
                    Category = MemoryCategory.Preferences,
                    Importance = MemoryImportance.High,
                    Pinned = true
                }
            ],
            SearchResults =
            [
                new MemorySearchResult(
                    Guid.NewGuid(),
                    "Uses C#",
                    MemoryCategory.Technical,
                    MemoryImportance.Medium,
                    0.92f,
                    false,
                    DateTimeOffset.UtcNow,
                    null)
            ]
        };

        var builder = CreateBuilder(memory);
        var (block, diagnostics) = await builder.BuildMemoryContextAsync(UserId, "What language do I use?");

        Assert.NotNull(block);
        Assert.Contains("Known User Information", block, StringComparison.Ordinal);
        Assert.Contains("Prefers Gemini", block, StringComparison.Ordinal);
        Assert.Contains("Uses C#", block, StringComparison.Ordinal);
        Assert.Contains("Do not invent user facts", block, StringComparison.Ordinal);
        Assert.True(diagnostics.RetrievedCount >= 2);
    }

    private static MemoryPromptBuilder CreateBuilder(FakeMemoryService memory) =>
        new(
            memory,
            Options.Create(new MemoryOptions()),
            NullLogger<MemoryPromptBuilder>.Instance);

    private sealed class FakeMemoryService : IMemoryService
    {
        public bool Enabled { get; init; } = true;
        public IReadOnlyList<Memory> Pinned { get; init; } = Array.Empty<Memory>();
        public IReadOnlyList<MemorySearchResult> SearchResults { get; init; } = Array.Empty<MemorySearchResult>();

        public Task<Memory> StoreMemoryAsync(
            Guid userId,
            string content,
            MemoryCategory category,
            MemoryImportance importance,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<MemorySearchResult>> SearchMemoryAsync(
            Guid userId,
            string query,
            int topK,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SearchResults);

        public Task<bool> DeleteMemoryAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Memory?> PinMemoryAsync(
            Guid userId,
            Guid memoryId,
            bool pinned,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Memory?> UpdateMemoryAsync(
            Guid userId,
            Guid memoryId,
            string content,
            MemoryCategory category,
            MemoryImportance importance,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<Memory>> ListMemoriesAsync(
            Guid userId,
            MemoryCategory? category,
            bool? pinnedOnly,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Pinned);

        public Task ExtractAndStoreFromUserMessageAsync(
            Guid userId,
            string userMessage,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<MemorySettingsSnapshot> GetSettingsAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MemorySettingsSnapshot(Enabled, 200, 365, Pinned.Count + SearchResults.Count));

        public Task UpdateSettingsAsync(
            Guid userId,
            MemorySettingsUpdate update,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<MemoryDeveloperStats> GetDeveloperStatsAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
