using Smartie.Contracts;

namespace Smartie.Shared.Models;

/// <summary>Attachment waiting to be sent with the next chat message.</summary>
public sealed class PendingChatAttachment
{
    public string Key { get; set; } = string.Empty;

    public required string Name { get; init; }

    public required string FileName { get; set; }

    public required string Extension { get; set; }

    public required long SizeBytes { get; set; }

    public required string SourceLabel { get; init; }

    public Guid? DocumentId { get; init; }

    public Guid? StagingId { get; set; }

    public bool IsUploading { get; set; }

    public string? Error { get; set; }

    public bool IsKnowledgeBase => DocumentId is not null;

    public static PendingChatAttachment FromKnowledgeBase(DocumentDto document) =>
        new()
        {
            Key = $"kb-{document.Id}",
            Name = document.Name,
            FileName = document.FileName,
            Extension = document.Extension,
            SizeBytes = document.SizeBytes,
            SourceLabel = "Knowledge Base",
            DocumentId = document.Id
        };

    public static PendingChatAttachment FromStaging(StagingChatAttachmentDto staged) =>
        new()
        {
            Key = $"local-{staged.StagingId}",
            Name = Path.GetFileNameWithoutExtension(staged.FileName),
            FileName = staged.FileName,
            Extension = staged.Extension,
            SizeBytes = staged.SizeBytes,
            SourceLabel = "Local Upload",
            StagingId = staged.StagingId
        };
}
