using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

public interface IAttachedDocumentPromptBuilder
{
    Task<string> BuildAugmentedUserMessageAsync(
        Guid userId,
        string userMessage,
        IReadOnlyList<MessageAttachment> attachments,
        CancellationToken cancellationToken = default);
}
