using Smartie.Application.Configuration;
using Smartie.Application.Services;

namespace Smartie.Tests;

public class FileSearchHelperTests : IDisposable
{
    private readonly string _root;
    private readonly FileIntegrationOptions _options = new();

    public FileSearchHelperTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "smartie-file-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void SearchFavoriteFolders_MatchesFileName()
    {
        File.WriteAllText(Path.Combine(_root, "Smartie_Test_Document.md"), "# Test");
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "hello");

        var results = FileSearchHelper.SearchFavoriteFolders(
            [_root],
            "smartie",
            showHidden: false,
            _options.AllowedExtensions,
            maxDepth: 2,
            maxResults: 10);

        Assert.Single(results);
        Assert.Equal("Smartie_Test_Document.md", results[0].FileName);
    }

    [Fact]
    public void SearchFavoriteFolders_FiltersUnsupportedExtensions()
    {
        File.WriteAllText(Path.Combine(_root, "Smartie.exe"), "binary");
        File.WriteAllText(Path.Combine(_root, "Smartie.txt"), "text");

        var results = FileSearchHelper.SearchFavoriteFolders(
            [_root],
            "smartie",
            showHidden: false,
            _options.AllowedExtensions,
            maxDepth: 1,
            maxResults: 10);

        Assert.Single(results);
        Assert.Equal(".txt", results[0].Extension);
    }

    [Fact]
    public void SearchFavoriteFolders_SkipsHiddenFilesWhenDisabled()
    {
        var hiddenPath = Path.Combine(_root, "Smartie.hidden.txt");
        File.WriteAllText(hiddenPath, "hidden");
        File.SetAttributes(hiddenPath, FileAttributes.Hidden);

        var results = FileSearchHelper.SearchFavoriteFolders(
            [_root],
            "smartie",
            showHidden: false,
            _options.AllowedExtensions,
            maxDepth: 1,
            maxResults: 10);

        Assert.Empty(results);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for temp test folder.
        }
    }
}
