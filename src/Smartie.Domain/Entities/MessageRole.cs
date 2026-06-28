namespace Smartie.Domain.Entities;

/// <summary>
/// Identifies who produced a given <see cref="Message"/>.
/// </summary>
public enum MessageRole
{
    System,
    User,
    Assistant,
    Tool
}
