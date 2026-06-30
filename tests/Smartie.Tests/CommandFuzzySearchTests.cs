using Smartie.Application.Services;

namespace Smartie.Tests;

public class CommandFuzzySearchTests
{
    [Theory]
    [InlineData("kb", "Knowledge Base")]
    [InlineData("doc", "Upload Document")]
    [InlineData("doc", "Search Documents")]
    [InlineData("mem", "Search Memory")]
    [InlineData("chat", "New Chat")]
    public void Score_MatchesExpectedTargets(string query, string target)
    {
        var score = CommandFuzzySearch.Score(query, target);
        Assert.True(score > 0);
    }

    [Fact]
    public void Score_UnrelatedQuery_ReturnsZero()
    {
        Assert.Equal(0, CommandFuzzySearch.Score("zzzz", "Dashboard"));
    }

    [Fact]
    public void ScoreCommand_UsesKeywords()
    {
        var command = CommandCatalog.GetStaticCommands().First(c => c.Id == "knowledge.open");
        var score = CommandFuzzySearch.ScoreCommand("kb", command);
        Assert.True(score > 0);
    }
}
