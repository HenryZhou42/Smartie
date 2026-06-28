using Smartie.Domain.Entities;

namespace Smartie.Application.Abstractions;

/// <summary>
/// Abstraction over the chat AI provider. Backed by Semantic Kernel, with the
/// concrete model provider (Gemini now; OpenAI/Ollama later) chosen by config.
/// </summary>
public interface IChatAiService
{
    /// <summary>
    /// Streams the assistant reply for the given conversation history as a
    /// sequence of text chunks. A system prompt is applied internally.
    /// </summary>
    IAsyncEnumerable<string> StreamReplyAsync(
        IReadOnlyList<Message> history,
        CancellationToken cancellationToken = default);
}
