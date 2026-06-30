using Smartie.Application.Services;
using Smartie.Domain.Entities;

namespace Smartie.Tests;

public class MemoryExtractorTests
{
    private readonly MemoryExtractor _extractor = new();

    [Theory]
    [InlineData("I prefer Gemini.", "Prefers Gemini", MemoryCategory.Preferences, MemoryImportance.High)]
    [InlineData("I'm working on Smartie.", "Building Smartie", MemoryCategory.Projects, MemoryImportance.High)]
    [InlineData("I use C#.", "Uses C#", MemoryCategory.Technical, MemoryImportance.Medium)]
    [InlineData("I'm interested in AI products.", "Interested in AI products", MemoryCategory.Interests, MemoryImportance.Medium)]
    [InlineData("I want Community Edition first.", "Wants Community Edition first", MemoryCategory.Goals, MemoryImportance.Medium)]
    [InlineData("I work in Edmonton.", "Works in Edmonton", MemoryCategory.Work, MemoryImportance.Medium)]
    [InlineData("Remember that I prefer dark mode.", "I prefer dark mode", MemoryCategory.Custom, MemoryImportance.High)]
    public void Extract_MatchesExpectedCandidate(
        string message,
        string expectedContent,
        MemoryCategory expectedCategory,
        MemoryImportance expectedImportance)
    {
        var results = _extractor.Extract(message);

        Assert.Contains(results, r =>
            r.Content.Equals(expectedContent, StringComparison.OrdinalIgnoreCase)
            && r.Category == expectedCategory
            && r.Importance == expectedImportance);
    }

    [Fact]
    public void Extract_EmptyMessage_ReturnsEmpty()
    {
        Assert.Empty(_extractor.Extract(""));
        Assert.Empty(_extractor.Extract("   "));
    }

    [Fact]
    public void Extract_DeduplicatesIdenticalContent()
    {
        var results = _extractor.Extract("I prefer Gemini and I prefer Gemini.");

        Assert.Single(results);
    }
}
