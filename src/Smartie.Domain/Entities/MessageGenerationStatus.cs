namespace Smartie.Domain.Entities;

/// <summary>
/// Whether an assistant message finished normally or was interrupted by the user.
/// </summary>
public enum MessageGenerationStatus
{
    Complete = 0,
    Stopped = 1
}
