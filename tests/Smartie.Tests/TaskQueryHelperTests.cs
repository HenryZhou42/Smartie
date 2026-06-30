using Smartie.Application.Services;
using Smartie.Domain.Entities;

namespace Smartie.Tests;

public class TaskQueryHelperTests
{
    [Fact]
    public void ApplyViewFilter_Today_ReturnsDueTodayTasks()
    {
        var today = DateTimeOffset.UtcNow;
        var tasks = new[]
        {
            new TaskItem { Title = "Today", DueDate = today, Archived = false },
            new TaskItem { Title = "Tomorrow", DueDate = today.AddDays(1), Archived = false },
            new TaskItem { Title = "Archived", DueDate = today, Archived = true }
        };

        var filtered = TaskQueryHelper.ApplyViewFilter(tasks, TaskViewFilter.Today, today).ToList();

        Assert.Single(filtered);
        Assert.Equal("Today", filtered[0].Title);
    }

    [Fact]
    public void ApplySearch_MatchesTitleAndDescription()
    {
        var tasks = new[]
        {
            new TaskItem { Title = "Build Smartie RAG", Description = "Implement semantic retrieval." },
            new TaskItem { Title = "Other", Description = "Nothing relevant" }
        };

        var filtered = TaskQueryHelper.ApplySearch(tasks, "semantic").ToList();

        Assert.Single(filtered);
        Assert.Equal("Build Smartie RAG", filtered[0].Title);
    }
}
