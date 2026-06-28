using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public sealed class AttachedDocumentPromptBuilder : IAttachedDocumentPromptBuilder
{
    private const string TruncationNote = "[Some attached content was truncated due to context limit.]";
    private const int PromptPreviewLength = 800;

    private readonly IDocumentRepository _documents;
    private readonly IDocumentTextExtractor _documentTextExtractor;
    private readonly IAttachmentTextExtractor _attachmentTextExtractor;
    private readonly IChatAttachmentStorage _chatAttachmentStorage;
    private readonly AttachedDocumentContextOptions _options;
    private readonly ILogger<AttachedDocumentPromptBuilder> _logger;

    public AttachedDocumentPromptBuilder(
        IDocumentRepository documents,
        IDocumentTextExtractor documentTextExtractor,
        IAttachmentTextExtractor attachmentTextExtractor,
        IChatAttachmentStorage chatAttachmentStorage,
        IOptions<AttachedDocumentContextOptions> options,
        ILogger<AttachedDocumentPromptBuilder> logger)
    {
        _documents = documents;
        _documentTextExtractor = documentTextExtractor;
        _attachmentTextExtractor = attachmentTextExtractor;
        _chatAttachmentStorage = chatAttachmentStorage;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> BuildAugmentedUserMessageAsync(
        Guid userId,
        string userMessage,
        IReadOnlyList<MessageAttachment> attachments,
        CancellationToken cancellationToken = default)
    {
        if (attachments.Count == 0)
        {
            return userMessage;
        }

        _logger.LogInformation(
            "Building augmented prompt with {AttachmentCount} attached file(s).",
            attachments.Count);

        var builder = new StringBuilder();
        builder.AppendLine("You are Smartie, an AI productivity assistant.");
        builder.AppendLine();
        builder.AppendLine("Attached Files:");
        builder.AppendLine();

        var remainingBudget = _options.MaxTotalCharacters;
        var fileIndex = 0;
        var extractedLengths = new List<(string Name, int Length)>();

        foreach (var attachment in attachments)
        {
            fileIndex++;
            var displayName = GetDisplayName(attachment);
            var sourceLabel = attachment.SourceType == MessageAttachmentSourceType.KnowledgeBase
                ? "Knowledge Base"
                : "Local Upload";

            builder.AppendLine($"File {fileIndex}:");
            builder.AppendLine($"Name: {displayName}");
            builder.AppendLine($"Source: {sourceLabel}");
            builder.AppendLine("Content:");

            if (remainingBudget <= 0)
            {
                builder.AppendLine(TruncationNote);
                builder.AppendLine();
                continue;
            }

            string content;
            try
            {
                content = await ExtractAttachmentTextAsync(userId, attachment, cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogInformation(
                    "Extracted {ExtractedLength} characters from {FileName} ({Source}).",
                    content.Length,
                    displayName,
                    sourceLabel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Text extraction failed for attachment {FileName}.", displayName);
                content = $"[Could not read file content: {ex.Message}]";
            }

            extractedLengths.Add((displayName, content.Length));

            if (content.Length > remainingBudget)
            {
                builder.AppendLine(content[..remainingBudget]);
                builder.AppendLine(TruncationNote);
                remainingBudget = 0;
            }
            else
            {
                builder.AppendLine(content);
                remainingBudget -= content.Length;
            }

            builder.AppendLine();
        }

        builder.AppendLine("User Question:");
        builder.AppendLine(userMessage.Trim());
        builder.AppendLine();
        builder.AppendLine("Instructions:");
        builder.AppendLine("- Use the attached files when relevant.");
        builder.AppendLine("- If the answer is not present in the attached files, say you cannot find it in the attached files.");
        builder.AppendLine("- Do not invent facts from the documents.");
        builder.AppendLine("- Mention which file was used.");

        var prompt = builder.ToString();
        _logger.LogInformation(
            "Augmented prompt built. Files: [{FileNames}], extracted lengths: [{ExtractedLengths}], final prompt length: {PromptLength}.",
            string.Join(", ", extractedLengths.Select(d => d.Name)),
            string.Join(", ", extractedLengths.Select(d => d.Length)),
            prompt.Length);

        if (_options.LogPromptPreview)
        {
            var preview = prompt.Length <= PromptPreviewLength
                ? prompt
                : prompt[..PromptPreviewLength] + "…";
            _logger.LogDebug("Augmented prompt preview:{NewLine}{PromptPreview}", Environment.NewLine, preview);
        }

        return prompt;
    }

    private async Task<string> ExtractAttachmentTextAsync(
        Guid userId,
        MessageAttachment attachment,
        CancellationToken cancellationToken)
    {
        if (attachment.SourceType == MessageAttachmentSourceType.KnowledgeBase)
        {
            if (attachment.DocumentId is not Guid documentId)
            {
                throw new InvalidOperationException("Knowledge Base attachment is missing DocumentId.");
            }

            var document = await _documents.FindAsync(documentId, userId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Document {documentId} was not found.");

            if (document.ExtractionStatus == DocumentExtractionStatus.Completed &&
                !string.IsNullOrWhiteSpace(document.ExtractedText))
            {
                return document.ExtractedText;
            }

            return await _documentTextExtractor.ExtractTextAsync(document, cancellationToken).ConfigureAwait(false);
        }

        var absolutePath = _chatAttachmentStorage.GetAbsolutePath(attachment.FilePath);
        return await _attachmentTextExtractor
            .ExtractFromFileAsync(absolutePath, attachment.Extension, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string GetDisplayName(MessageAttachment attachment)
    {
        if (!string.IsNullOrWhiteSpace(attachment.Document?.Name))
        {
            return attachment.Document.Name;
        }

        if (!string.IsNullOrWhiteSpace(attachment.OriginalFileName))
        {
            return Path.GetFileNameWithoutExtension(attachment.OriginalFileName);
        }

        return attachment.StoredFileName;
    }
}
