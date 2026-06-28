namespace Smartie.Contracts;

/// <summary>A file attached to a chat message.</summary>
public sealed record MessageAttachmentDto(
    Guid Id,
    Guid? DocumentId,
    string Name,
    string FileName,
    string Extension,
    long SizeBytes,
    string SourceType,
    string SourceLabel);
