namespace Smartie.Application.Abstractions;

public interface ISemanticSearchService
{
    Task<SemanticSearchResultSet> SearchAsync(
        Guid userId,
        string query,
        int topK,
        CancellationToken cancellationToken = default);
}
