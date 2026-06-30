namespace Smartie.Application.Abstractions;

public interface IOnboardingRepository
{
    Task<bool> GetHasCompletedOnboardingAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SetCompletedOnboardingAsync(Guid userId, CancellationToken cancellationToken = default);

    Task MarkDocumentAsSampleAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);
}
