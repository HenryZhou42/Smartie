namespace Smartie.Application.Abstractions;

/// <summary>
/// Raised when the underlying AI provider fails in a way the user should be told
/// about cleanly (rather than surfacing a raw provider/transport exception).
/// </summary>
public class AiServiceException : Exception
{
    public AiServiceException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Raised when the AI provider rejects the request because of rate limiting or
/// exhausted quota (HTTP 429). Typically transient.
/// </summary>
public sealed class AiRateLimitedException : AiServiceException
{
    public AiRateLimitedException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
